using System.Collections.Generic;
using MeridianServer.TransportLayer.ApplicationProvider;
using MeridianServer.TransportLayer.Interfaces;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServer.TransportLayer.Models
{
    public class Transport : ITransport
    {
	    private readonly ILogger _logger;
        private readonly List<IApplicationProvider> _applicationProviders;

        public Transport(MeridianApplicationConfiguration[] applicationConfigurations, ILogger logger)
        {
            _logger = logger;
            _applicationProviders = new List<IApplicationProvider>(applicationConfigurations.Length);

			foreach (var configuration in applicationConfigurations)
            {
	            var transportParams = new TransportParams(configuration.Port);
	            var applicationProvider =
		            new BaseApplicationProvider(configuration.Id, transportParams, configuration.MeridianApplication, logger);

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

        public void Discard()
        {
	        foreach (var provider in _applicationProviders)
	        {
				if (provider.IsStarted)
				{
					provider.Stop();
				}
			}
        }
    }
}