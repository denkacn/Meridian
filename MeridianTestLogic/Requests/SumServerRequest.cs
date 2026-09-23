using System;
using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Client;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianTestContracts.Requests;
using MeridianTestContracts.Responses;

namespace MeridianTestLogic.Requests
{
    [ServerRequest(typeof(SumRequest))]
    public sealed class SumServerRequest : SumRequest, IServerDataNetworkRequest
    {
        public bool IsAsync => false;

        public IDataNetworkResponse Execute(IUserClient client)
        {
            return new SumResponse(this, A + B);
        }

        public void Execute(IUserClient client, Action<IDataNetworkResponse> executeResponse)
        {
            executeResponse?.Invoke(Execute(client));
        }
    }
}
