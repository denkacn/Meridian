using System;
using MeridianServer.TransportLayer.NetCoreServerDomain.Sessions;

namespace MeridianServer.TransportLayer.Interfaces
{
    public interface IServer
    {
        public event EventHandler StartedEventHandler;
        public event EventHandler StoppedEventHandler;
        public event EventHandler<ServerPeerSession> ConnectedEventHandler;
        bool IsStarted { get; }
        IServer Setup();

        bool Start();
        bool Stop();
    }
}