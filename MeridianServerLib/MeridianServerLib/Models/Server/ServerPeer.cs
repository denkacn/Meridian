using System;
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
            ServerPeerSession.ReceivedEventHandler += OnReceivedMessage;
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

        protected virtual void Discard()
        {
            ServerPeerSession.ConnectedEventHandler -= OnConnected;
            ServerPeerSession.DisconnectedEventHandler -= OnDisconnected;
            ServerPeerSession.ReceivedEventHandler -= OnReceivedMessage;
        }
    }
}