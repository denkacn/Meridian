using MeridianServer.TransportLayer.Interfaces;
using MeridianServer.TransportLayer.Models;
using MeridianServer.TransportLayer.NetCoreServerDomain;
using MeridianServer.TransportLayer.NetCoreServerDomain.Sessions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.LogsLayer.Interfaces;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MeridianServer.TransportLayer.ApplicationProvider
{
	public class BaseApplicationProvider : IApplicationProvider
	{
		public bool IsStarted => _server.IsStarted;

		private readonly string _id;
		private readonly string _path;

		private readonly IServer _server;
		private readonly ILogger _logger;
		private readonly IMeridianApplication _applicationLogic;
		private Task _setupTask = Task.CompletedTask;
		private CancellationTokenSource _applicationCancellationTokenSource = new CancellationTokenSource();

		public BaseApplicationProvider(string id, TransportParams transportParams, IMeridianApplication applicationLogic, string path, ILogger logger)
		{
			_id = id;
			_path = path;

			_logger = logger;

			_applicationLogic = applicationLogic;
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

		public void Discard()
		{
			if (IsStarted)
			{
				_server.Stop();
			}
		}

		private void OnServerStarted(object sender, EventArgs e)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerStarted");

			ResetApplicationCancellationTokenSource();
			_setupTask = SetupApplicationAsync(_applicationCancellationTokenSource.Token);
		}

		private void OnServerStopped(object sender, EventArgs e)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerStopped");

			_applicationCancellationTokenSource.Cancel();
			_ = DiscardApplicationAsync();
		}

		private void OnServerConnected(object sender, ServerPeerSession peerSession)
		{
			_logger?.Log("[BaseApplicationProvider] OnServerConnected");

			_ = InitServerPeerAsync(peerSession, _applicationCancellationTokenSource.Token);
		}

		private async Task SetupApplicationAsync(CancellationToken cancellationToken)
		{
			try
			{
				if (_applicationLogic is IAsyncMeridianApplication asyncApplication)
				{
					await asyncApplication.SetupAsync(_id, _path, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					_applicationLogic.Setup(_id, _path);
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

		private async Task InitServerPeerAsync(IServerPeerSession peerSession, CancellationToken cancellationToken)
		{
			try
			{
				await _setupTask.ConfigureAwait(false);
				if (_applicationLogic is IAsyncMeridianApplication asyncApplication)
				{
					await asyncApplication.InitServerPeerAsync(peerSession, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					_applicationLogic.InitServerPeer(peerSession);
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

		private async Task DiscardApplicationAsync()
		{
			try
			{
				if (_applicationLogic is IAsyncMeridianApplication asyncApplication)
				{
					await asyncApplication.DiscardAsync(CancellationToken.None).ConfigureAwait(false);
				}
				else
				{
					_applicationLogic.Discard();
				}
			}
			catch (Exception ex)
			{
				_logger?.LogError($"[BaseApplicationProvider] ({_id}) Application discard error", ex);
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
	}
}
