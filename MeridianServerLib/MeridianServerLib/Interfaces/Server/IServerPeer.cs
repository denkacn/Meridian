using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.Interfaces.Server
{
    public interface IServerPeer
    {
        string Id { get; }
        IServerPeerSession ServerPeerSession { get; }
        void Send(OperationData operationData);
    }
}