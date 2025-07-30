using MeridianServerLib.Models.Client;
using MeridianServerLib.Models.Operations;
using System;

namespace MeridianServerLib.Interfaces.Client
{
    public interface IClientPeer
    {
        public event Action<NetworkClientConnectionStatus> ClientConnectionStatusChanged;
        bool IsConnected { get; } 
        bool Connect();
        bool Disconnect();
        void Discard();
        bool Send(OperationData operation);
        void SetOperationsReceiver(IOperationsReceiver receiver);
    }
}