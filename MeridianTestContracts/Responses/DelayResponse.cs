using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Responses
{
    public class DelayResponse : BaseDataResponse
    {
        [RequestField(10)]
        public string Message;

        public DelayResponse() : base((int)TestRequestType.Delay) { }

        public DelayResponse(BaseDataRequest request) : base(request, 0) => Message = "delayed";
    }
}
