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
        
        private readonly ILogger _logger;

        public ServerBase(IPAddress address, int port, ILogger logger) : base(address, port)
        {
	        _logger = logger;
        }

        public IServer Setup()
        {
	        OptionKeepAlive = true;

	        return this;
        }

        protected override void OnStarted()
        {
            StartedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnStopped()
        {
            StoppedEventHandler?.Invoke(this, EventArgs.Empty);
        }

        protected override TcpSession CreateSession()
        {
            var session = new ServerPeerSession(this, _logger);
            return session;
        }

        protected override void OnError(SocketError error)
        {
            _logger?.Log($"[ServerBase] Server caught an error with code {error}");
        }

        protected override void OnConnected(TcpSession session)
        {
            _logger?.Log($"[ServerBase] OnConnected " + session.Id);
            ConnectedEventHandler?.Invoke(this, (ServerPeerSession)session);
        }
    }
}