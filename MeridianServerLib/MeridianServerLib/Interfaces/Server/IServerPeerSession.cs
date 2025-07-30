using System;
using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.Interfaces.Server
{
    public interface IServerPeerSession
    {
        string SessionId { get; }
        
        event EventHandler ConnectedEventHandler;
        event EventHandler DisconnectedEventHandler;
        event EventHandler<OperationData> ReceivedEventHandler;
        event EventHandler<string> ErrorEventHandler;
        void Send(OperationData operationData);
		void Update();
	}
}