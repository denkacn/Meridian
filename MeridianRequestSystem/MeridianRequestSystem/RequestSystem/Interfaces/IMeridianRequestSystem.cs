using System.Threading;
using System.Threading.Tasks;
using MeridianServerLib.Models.Operations;
using MeridianRequestSystem.RequestSystem.Client;

namespace MeridianRequestSystem.RequestSystem.Interfaces
{
    public interface IMeridianRequestSystem
    {
        Task HandleIncomingAsync(
            OperationData messageData,
            IUserClient client,
            CancellationToken cancellationToken = default);
    }
}
