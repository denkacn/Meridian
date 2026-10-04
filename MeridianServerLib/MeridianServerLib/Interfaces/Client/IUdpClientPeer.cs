using System.Threading.Tasks;
using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.Interfaces.Client
{
    public interface IUdpClientPeer
    {
        bool IsUdpReady { get; }
        bool EnableUdp();
        Task<bool> SendUdpAsync(OperationData operation, UdpDeliveryMode mode);
    }
}
