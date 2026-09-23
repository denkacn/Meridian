using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MeridianServer.TransportLayer.ApplicationProvider;
using MeridianServer.TransportLayer.Interfaces;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Server;

namespace MeridianServer.TransportLayer.Models
{
	public class Transport : ITransport
	{
		private readonly ILogger _logger;
		private readonly List<IApplicationProvider> _applicationProviders;

		public Transport(
			MeridianApplicationConfiguration[] applicationConfigurations,
			ILogger logger,
			Action<OutsideCommandType> applicationCommandHandler)
		{
			_logger = logger;
			_applicationProviders = new List<IApplicationProvider>(applicationConfigurations.Length);

			foreach (var configuration in applicationConfigurations)
			{
				var transportParams = new TransportParams(configuration.Port);
				var applicationProvider =
					new BaseApplicationProvider(
						configuration.Id,
						transportParams,
						configuration.Path,
						Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime-layers"),
						applicationCommandHandler,
						logger);

				_applicationProviders.Add(applicationProvider);
			}
		}

		public void Start()
		{
			_applicationProviders.ForEach(a => a.Start());
		}

		public void Stop()
		{
			_applicationProviders.ForEach(a => a.Stop());
		}

		public async Task ReloadApplicationAsync(string id)
		{
			var provider = _applicationProviders.FirstOrDefault(p => p.Id == id);
			if (provider == null)
			{
				_logger?.Log($"[Transport] Application provider was not found: {id}");
				return;
			}

			await provider.ReloadAsync().ConfigureAwait(false);
		}

		public void Discard()
		{
			foreach (var provider in _applicationProviders)
			{
				provider.Discard();
			}
		}
	}
}
