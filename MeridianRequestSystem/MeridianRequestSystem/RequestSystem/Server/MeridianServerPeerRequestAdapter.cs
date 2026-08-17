using System.Threading;
using System.Threading.Tasks;
using MeridianServerLib.Models.Operations;
using MeridianRequestSystem.RequestSystem.Client;
using MeridianRequestSystem.RequestSystem.Interfaces;

namespace MeridianRequestSystem.RequestSystem.Server
{
    public sealed class MeridianServerPeerRequestAdapter
    {
        private readonly IMeridianRequestSystem _requestSystem;
        private readonly IUserClient _client;

        public MeridianServerPeerRequestAdapter(IMeridianRequestSystem requestSystem, IUserClient client)
        {
            _requestSystem = requestSystem;
            _client = client;
        }

        public Task OnReceivedMessageAsync(
            object sender,
            OperationData messageData,
            CancellationToken cancellationToken)
        {
            return _requestSystem.HandleIncomingAsync(messageData, _client, cancellationToken);
        }
    }
}
