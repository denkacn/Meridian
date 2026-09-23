using System;
using System.Collections.Generic;
using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Client;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianTestContracts.Requests;
using MeridianTestContracts.Responses;

namespace MeridianTestLogic.Requests
{
    [ServerRequest(typeof(EchoRequest))]
    public sealed class EchoServerRequest : EchoRequest, IServerDataNetworkRequest
    {
        public bool IsAsync => false;

        public IDataNetworkResponse Execute(IUserClient client)
        {
            return new EchoResponse(this, Message);
        }

        public void Execute(IUserClient client, Action<IDataNetworkResponse> executeResponse)
        {
            executeResponse?.Invoke(Execute(client));
        }

        public void Map(Dictionary<byte, object> package)
        {
            RequestId = package.TryGetValue(1, out var requestId) ? requestId as string : null;
            Message = package.TryGetValue(10, out var message) ? message as string : null;
        }
    }
}
