using System;
using System.Threading.Tasks;
using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Client;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianTestContracts.Requests;
using MeridianTestContracts.Responses;

namespace MeridianTestLogic.Requests
{
    [ServerRequest(typeof(DelayRequest))]
    public sealed class DelayServerRequest : DelayRequest, IServerDataNetworkRequest
    {
        public bool IsAsync => true;

        public IDataNetworkResponse Execute(IUserClient client) => new DelayResponse(this);

        public void Execute(IUserClient client, Action<IDataNetworkResponse> executeResponse)
        {
            _ = CompleteAsync(executeResponse);
        }

        private async Task CompleteAsync(Action<IDataNetworkResponse> executeResponse)
        {
            await Task.Delay(Math.Min(Math.Max(DelayMilliseconds, 0), 1000)).ConfigureAwait(false);
            executeResponse?.Invoke(new DelayResponse(this));
        }
    }
}
