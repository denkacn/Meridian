using System.Threading.Tasks;
using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.Interfaces.Server
{
    public interface IUdpServerPeerSession
    {
        bool IsUdpReady { get; }
        void AllowUdpOperation(byte operationCode);
        Task<bool> SendUdpAsync(OperationData operation, UdpDeliveryMode mode);
    }
}
