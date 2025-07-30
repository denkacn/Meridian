using System;
using System.Net;
using MeridianServer.TransportLayer.Interfaces;
using MeridianServer.TransportLayer.NetCoreServerDomain;
using MeridianServer.TransportLayer.NetCoreServerDomain.Sessions;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.LogsLayer.Interfaces;

namespace MeridianServer.TransportLayer.Models
{
    public class Transport : ITransport
    {
        private readonly IServer _server;
        private readonly ILogger _logger;
        private readonly IMeridianApplication _applicationLogic;
        
        public Transport(TransportParams transportParams, IMeridianApplication applicationLogic, ILogger logger)
        {
            _logger = logger;
            _applicationLogic = applicationLogic;
            _server = new ServerBase(IPAddress.Any, transportParams.Port, _logger).Setup();
            
            _server.StartedEventHandler += OnServerStarted;
            _server.ConnectedEventHandler += OnServerConnected;
            _server.StoppedEventHandler += OnServerStopped;
        }

        public void Start()
        {
            _server.Start();
        }

        public void Stop()
        {
            _server.Stop();
        }

        public void Discard()
        {
            if (_server.IsStarted)
            {
                _server.Stop();
            }
        }
        
        private void OnServerStarted(object sender, EventArgs e)
        {
            _logger?.Log("[Transport] OnServerStarted");
            _applicationLogic.Setup();
        }

        private void OnServerStopped(object sender, EventArgs e)
        {
            _logger?.Log("[Transport] OnServerStopped");
            _applicationLogic.Discard();
        }

        private void OnServerConnected(object sender, ServerPeerSession peerSession)
        {
            _logger?.Log("[Transport] OnServerConnected");
            _applicationLogic.InitServerPeer(peerSession);
        }
    }
}