using System;
using System.Collections.Generic;
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

        public void Map(Dictionary<byte, object> package)
        {
            RequestId = package.TryGetValue(1, out var requestId) ? requestId as string : null;
            A = package.TryGetValue(10, out var a) ? Convert.ToInt32(a) : 0;
            B = package.TryGetValue(11, out var b) ? Convert.ToInt32(b) : 0;
        }
    }
}
