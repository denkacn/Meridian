using System;
using System.Threading;
using System.Threading.Tasks;
using MeridianServerLib.Interfaces.Server;
using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.Models.Server
{
    public class ServerPeer : IServerPeer
    {
        public string Id => ServerPeerSession.SessionId;
        public IServerPeerSession ServerPeerSession { get; }

        public ServerPeer(IServerPeerSession serverPeerSession)
        {
            ServerPeerSession = serverPeerSession;

            ServerPeerSession.ConnectedEventHandler += OnConnected;
            ServerPeerSession.DisconnectedEventHandler += OnDisconnected;
            ServerPeerSession.ReceivedAsyncEventHandler += OnReceivedMessageAsync;
        }

        public void Send(OperationData operationData)
        {
            ServerPeerSession.Send(operationData);
        }

        public void Update()
        {
            ServerPeerSession.Update();
        }

        protected virtual void OnConnected(object sender, EventArgs e){}
        protected virtual void OnDisconnected(object sender, EventArgs e){}
        protected virtual void OnReceivedMessage(object sender, OperationData messageData){}

        protected virtual Task OnReceivedMessageAsync(object sender, OperationData messageData, CancellationToken cancellationToken)
        {
            OnReceivedMessage(sender, messageData);
            return Task.CompletedTask;
        }

        protected virtual void Discard()
        {
            ServerPeerSession.ConnectedEventHandler -= OnConnected;
            ServerPeerSession.DisconnectedEventHandler -= OnDisconnected;
            ServerPeerSession.ReceivedAsyncEventHandler -= OnReceivedMessageAsync;
        }
    }
}
