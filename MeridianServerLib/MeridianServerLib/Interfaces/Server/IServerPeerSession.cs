using System;
using System.Threading;
using System.Threading.Tasks;
using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.Interfaces.Server
{
    public delegate Task AsyncOperationReceivedEventHandler(object sender, OperationData operationData, CancellationToken cancellationToken);

    public interface IServerPeerSession
    {
        string SessionId { get; }

        event EventHandler ConnectedEventHandler;
        event EventHandler DisconnectedEventHandler;
        event EventHandler<OperationData> ReceivedEventHandler;
        event AsyncOperationReceivedEventHandler ReceivedAsyncEventHandler;
        event EventHandler<string> ErrorEventHandler;
        void Send(OperationData operationData);
        void Update();
    }
}
