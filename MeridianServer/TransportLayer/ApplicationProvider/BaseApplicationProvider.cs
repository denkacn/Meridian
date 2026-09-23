using MeridianServer.ExternalLayer.Controllers;
using MeridianServer.ExternalLayer.Models;
using MeridianServer.TransportLayer.Interfaces;
using MeridianServer.TransportLayer.Models;
using MeridianServer.TransportLayer.NetCoreServerDomain;
using MeridianServer.TransportLayer.NetCoreServerDomain.Sessions;
using MeridianServerLib.Exceptions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Server;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MeridianServer.TransportLayer.ApplicationProvider
{
	public class BaseApplicationProvider : IApplicationProvider
	{
		private const int ReloadDebounceMilliseconds = 700;

		public bool IsStarted => _server.IsStarted;
		public string Id => _id;

		private readonly string _id;
		private readonly string _path;
		private readonly string _shadowRootDirectory;
		private readonly Action<OutsideCommandType> _applicationCommandHandler;
		private readonly SemaphoreSlim _reloadLock = new SemaphoreSlim(1, 1);

		private readonly IServer _server;
		private readonly ILogger _logger;
		private readonly FileSystemWatcher _applicationWatcher;

		private ExternalApplicationLoadHandle _applicationHandle;
		private IMeridianApplication _applicationLogic;
		private Task _setupTask = Task.CompletedTask;
		private Task _discardTask = Task.CompletedTask;
		private CancellationTokenSource _applicationCancellationTokenSource = new CancellationTokenSource();
		private CancellationTokenSource _reloadDebounceCancellationTokenSource;
		private bool _isDiscarded;

		public BaseApplicationProvider(
			string id,
			TransportParams transportParams,
			string path,
			string shadowRootDirectory,
			Action<OutsideCommandType> applicationCommandHandler,
			ILogger logger)
		{
			_id = id;
			_path = path;
			_shadowRootDirectory = shadowRootDirectory;
			_applicationCommandHandler = applicationCommandHandler;
			_logger = logger;

			LoadApplication();
			_applicationWatcher = CreateApplicationWatcher();
			_server = new ServerBase(_id, IPAddress.Any, transportParams.Port, _logger).Setup();

			_server.StartedEventHandler += OnServerStarted;
			_server.ConnectedEventHandler += OnServerConnected;
			_server.StoppedEventHandler += OnServerStopped;
		}

		public void Start()
		{
			if (!IsStarted) _server.Start();
		}

		public void Stop()
		{
			_server.Stop();
		}

		public async Task ReloadAsync()
		{
			await _reloadLock.WaitAsync().ConfigureAwait(false);

			try
			{
				if (_isDiscarded)
				{
					return;
				}

				_logger?.Log($"[BaseApplicationProvider] ({_id}) Reload layer");

				var wasStarted = IsStarted;
				if (wasStarted)
				{
					_server.Stop();
				}
				else
				{
					_applicationCancellationTokenSource.Cancel();
					_discardTask = DiscardApplicationAsync(_applicationLogic);
				}

				await _discardTask.ConfigureAwait(false);
				UnloadApplication();
				await WaitUntilSourceDllReadyAsync(CancellationToken.None).ConfigureAwait(false);
				LoadApplication();

				if (wasStarted)
				{
					_server.Start();
				}
			}
			catch (Exception ex)
			{
				_logger?.LogError($"[BaseApplicationProvider] ({_id}) Reload layer error", ex);
			}
			finally
			{
				_reloadLock.Release();
			}
		}

		public void Discard()
		{
			_isDiscarded = true;
			_applicationWatcher?.Dispose();
			_reloadDebounceCancellationTokenSource?.Cancel();
			_reloadDebounceCancellationTokenSource?.Dispose();

			if (IsStarted)
			{
				_server.Stop();
			}
			else
			{
				_applicationCancellationTokenSource.Cancel();
			}

			try
			{
				_discardTask.GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				_logger?.LogError($"[BaseApplicationProvider] ({_id}) Application discard wait error", ex);
			}

			UnloadApplication();
			_applicationCancellationTokenSource.Dispose();

			_server.StartedEventHandler -= OnServerStarted;
			_server.ConnectedEventHandler -= OnServerConnected;
			_server.StoppedEventHandler -= OnServerStopped;

			if (_server is IDisposable disposableServer)
			{
				disposableServer.Dispose();
			}
		}

		private void OnServerStarted(object sender, EventArgs e)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerStarted");

			ResetApplicationCancellationTokenSource();
			var applicationLogic = _applicationLogic;
			_setupTask = SetupApplicationAsync(applicationLogic, _applicationCancellationTokenSource.Token);
		}

		private void OnServerStopped(object sender, EventArgs e)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerStopped");

			_applicationCancellationTokenSource.Cancel();
			var applicationLogic = _applicationLogic;
			_discardTask = DiscardApplicationAsync(applicationLogic);
		}

		private void OnServerConnected(object sender, ServerPeerSession peerSession)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerConnected");

			var applicationLogic = _applicationLogic;
			var setupTask = _setupTask;
			var cancellationToken = _applicationCancellationTokenSource.Token;
			_ = InitServerPeerAsync(applicationLogic, setupTask, peerSession, cancellationToken);
		}

		private async Task SetupApplicationAsync(IMeridianApplication applicationLogic, CancellationToken cancellationToken)
		{
			try
			{
				if (applicationLogic is IAsyncMeridianApplication asyncApplication)
				{
					await asyncApplication.SetupAsync(_id, _path, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					applicationLogic.Setup(_id, _path);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				_logger?.LogError($"[BaseApplicationProvider] ({_id}) Application setup error", ex);
			}
		}

		private async Task InitServerPeerAsync(
			IMeridianApplication applicationLogic,
			Task setupTask,
			IServerPeerSession peerSession,
			CancellationToken cancellationToken)
		{
			try
			{
				await setupTask.ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();

				if (applicationLogic is IAsyncMeridianApplication asyncApplication)
				{
					await asyncApplication.InitServerPeerAsync(peerSession, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					applicationLogic.InitServerPeer(peerSession);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				_logger?.LogError($"[BaseApplicationProvider] ({_id}) Application peer init error", ex);
			}
		}

		private async Task DiscardApplicationAsync(IMeridianApplication applicationLogic)
		{
			if (applicationLogic == null)
			{
				return;
			}

			try
			{
				if (applicationLogic is IAsyncMeridianApplication asyncApplication)
				{
					await asyncApplication.DiscardAsync(CancellationToken.None).ConfigureAwait(false);
				}
				else
				{
					applicationLogic.Discard();
				}
			}
			catch (Exception ex)
			{
				_logger?.LogError($"[BaseApplicationProvider] ({_id}) Application discard error", ex);
			}
		}

		private void LoadApplication()
		{
			try
			{
				_applicationHandle = ExternalApplicationController.LoadExternalApplication(_path, _id, _shadowRootDirectory);
			}
			catch (MeridianExternalLogicException ex)
			{
				throw new MeridianExternalLogicException($"Failed to load Meridian layer '{_id}' from '{_path}'.", ex);
			}

			_applicationLogic = _applicationHandle.Application;
			_applicationLogic.MeridianApplicationCommand += OnMeridianApplicationCommand;

			_logger?.Log($"[BaseApplicationProvider] ({_id}) Loaded layer from {_path}");
		}

		private void UnloadApplication()
		{
			var loadContext = _applicationHandle?.LoadContext;
			var loadContextReference = loadContext == null ? null : new WeakReference(loadContext, trackResurrection: false);
			var shadowDirectory = _applicationHandle?.ShadowDirectory;

			if (_applicationLogic != null)
			{
				_applicationLogic.MeridianApplicationCommand -= OnMeridianApplicationCommand;
			}

			_applicationLogic = null;
			_applicationHandle?.Dispose();
			_applicationHandle = null;
			loadContext = null;

			if (loadContextReference != null)
			{
				WaitForUnload(loadContextReference);
			}

			TryDeleteShadowDirectory(shadowDirectory);
		}

		private FileSystemWatcher CreateApplicationWatcher()
		{
			var directory = Path.GetDirectoryName(_path);
			var fileName = Path.GetFileName(_path);

			if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
			{
				throw new MeridianExternalLogicException($"External application DLL path is invalid: {_path}");
			}

			var watcher = new FileSystemWatcher(directory, fileName)
			{
				NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
				EnableRaisingEvents = true
			};

			watcher.Changed += OnApplicationDllChanged;
			watcher.Created += OnApplicationDllChanged;
			watcher.Renamed += OnApplicationDllRenamed;
			watcher.Error += OnApplicationWatcherError;
			return watcher;
		}

		private void OnApplicationDllChanged(object sender, FileSystemEventArgs e)
		{
			ScheduleReload();
		}

		private void OnApplicationDllRenamed(object sender, RenamedEventArgs e)
		{
			ScheduleReload();
		}

		private void OnApplicationWatcherError(object sender, ErrorEventArgs e)
		{
			_logger?.LogError($"[BaseApplicationProvider] ({_id}) Application watcher error", e.GetException());
		}

		private void ScheduleReload()
		{
			if (_isDiscarded)
			{
				return;
			}

			_reloadDebounceCancellationTokenSource?.Cancel();
			_reloadDebounceCancellationTokenSource?.Dispose();
			_reloadDebounceCancellationTokenSource = new CancellationTokenSource();
			var cancellationToken = _reloadDebounceCancellationTokenSource.Token;

			_ = Task.Run(async () =>
			{
				try
				{
					await Task.Delay(ReloadDebounceMilliseconds, cancellationToken).ConfigureAwait(false);
					await WaitUntilSourceDllReadyAsync(cancellationToken).ConfigureAwait(false);
					await ReloadAsync().ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
				}
				catch (Exception ex)
				{
					_logger?.LogError($"[BaseApplicationProvider] ({_id}) Scheduled reload error", ex);
				}
			}, cancellationToken);
		}

		private async Task WaitUntilSourceDllReadyAsync(CancellationToken cancellationToken)
		{
			for (var attempt = 0; attempt < 20; attempt++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					using (File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
					}

					return;
				}
				catch (IOException)
				{
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
				}
				catch (UnauthorizedAccessException)
				{
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		private void ResetApplicationCancellationTokenSource()
		{
			if (!_applicationCancellationTokenSource.IsCancellationRequested)
			{
				return;
			}

			_applicationCancellationTokenSource.Dispose();
			_applicationCancellationTokenSource = new CancellationTokenSource();
		}

		private void OnMeridianApplicationCommand(OutsideCommandType command)
		{
			_applicationCommandHandler?.Invoke(command);
		}

		private static void WaitForUnload(WeakReference loadContextReference)
		{
			for (var i = 0; loadContextReference.IsAlive && i < 10; i++)
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				Thread.Sleep(50);
			}
		}

		private static void TryDeleteShadowDirectory(string shadowDirectory)
		{
			if (string.IsNullOrWhiteSpace(shadowDirectory))
			{
				return;
			}

			try
			{
				if (Directory.Exists(shadowDirectory))
				{
					Directory.Delete(shadowDirectory, recursive: true);
				}
			}
			catch
			{
			}
		}
	}
}
