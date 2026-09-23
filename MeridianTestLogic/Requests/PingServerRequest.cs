using System;
using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Client;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianTestContracts.Requests;
using MeridianTestContracts.Responses;

namespace MeridianTestLogic.Requests
{
    [ServerRequest(typeof(PingRequest))]
    public sealed class PingServerRequest : PingRequest, IServerDataNetworkRequest
    {
        public bool IsAsync => false;

        public IDataNetworkResponse Execute(IUserClient client)
        {
            return new PingResponse(this, "pong from Meridian test logic");
        }

        public void Execute(IUserClient client, Action<IDataNetworkResponse> executeResponse)
        {
            executeResponse?.Invoke(Execute(client));
        }
    }
}
