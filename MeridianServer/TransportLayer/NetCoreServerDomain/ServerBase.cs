using System;
using System.Net;
using System.Net.Sockets;
using MeridianServer.TransportLayer.Interfaces;
using MeridianServer.TransportLayer.NetCoreServerDomain.Sessions;
using MeridianServerLib.LogsLayer.Interfaces;
using NetCoreServer;

namespace MeridianServer.TransportLayer.NetCoreServerDomain
{
    public class ServerBase : TcpServer, IServer
    {
        public event EventHandler StartedEventHandler;
        public event EventHandler StoppedEventHandler;
        public event EventHandler<ServerPeerSession> ConnectedEventHandler;

		private readonly string _id;
		private readonly ILogger _logger;
		private readonly UdpServerGateway _udpGateway;
		internal UdpServerGateway UdpGateway => _udpGateway;

        public ServerBase(string id, IPAddress address, int port, ILogger logger) : base(address, port)
        {
            _id = id;
	        _logger = logger;
	        _udpGateway = new UdpServerGateway(address, port, logger);
        }

        public IServer Setup()
        {
	        OptionKeepAlive = true;

	        return this;
        }

        protected override void OnStarted()
        {
            try
            {
                if (!_udpGateway.Start())
                    _logger?.Log("[ServerBase] UDP listener could not start; TCP remains available");
            }
            catch (Exception ex)
            {
                _logger?.LogError("[ServerBase] UDP listener could not start", ex);
            }
            StartedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnStopped()
        {
            _udpGateway.Stop();
            StoppedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override TcpSession CreateSession()
        {
            var session = new ServerPeerSession(_id, this, _udpGateway, _logger);
            return session;
        }

        protected override void Dispose(bool disposingManagedResources)
        {
            if (disposingManagedResources) _udpGateway.Dispose();
            base.Dispose(disposingManagedResources);
        }

        protected override void OnError(SocketError error)
        {
            _logger?.Log($"[ServerBase] ({_id}) Server caught an error with code {error}");
        }

        protected override void OnConnected(TcpSession session)
        {
            _logger?.Log($"[ServerBase] ({_id}) OnConnected " + session.Id);
            ConnectedEventHandler?.Invoke(this, (ServerPeerSession)session);
        }
    }
}
