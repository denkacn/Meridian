using System;
using System.Collections.Generic;
using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Responses
{
    public class PingResponse : BaseDataResponse
    {
        [RequestField(10)]
        public string Message;

        [RequestField(11)]
        public string ServerTimeUtc;

        public PingResponse()
            : base((int)TestRequestType.Ping)
        {
        }

        public PingResponse(BaseDataRequest request, string message)
            : base(request, 0)
        {
            Message = message;
            ServerTimeUtc = DateTime.UtcNow.ToString("O");
        }

        public override void Map(Dictionary<byte, object> package)
        {
            ResponseMap.ReadBase(this, package);
            Message = ResponseMap.ReadString(package, 10);
            ServerTimeUtc = ResponseMap.ReadString(package, 11);
        }
    }
}
