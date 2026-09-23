using System.Collections.Generic;
using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Responses
{
    public class EchoResponse : BaseDataResponse
    {
        [RequestField(10)]
        public string Message;

        [RequestField(11)]
        public int Length;

        public EchoResponse()
            : base((int)TestRequestType.Echo)
        {
        }

        public EchoResponse(BaseDataRequest request, string message)
            : base(request, 0)
        {
            Message = message;
            Length = message?.Length ?? 0;
        }

        public override void Map(Dictionary<byte, object> package)
        {
            ResponseMap.ReadBase(this, package);
            Message = ResponseMap.ReadString(package, 10);
            Length = ResponseMap.ReadInt(package, 11);
        }
    }
}
