using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Requests
{
    [RequestBase(TestOperationCodes.RequestSystem, true)]
    public class EchoRequest : BaseDataRequest
    {
        [RequestField(10)]
        public string Message;

        public EchoRequest()
            : base((int)TestRequestType.Echo)
        {
        }

        public EchoRequest(string message)
            : this()
        {
            Message = message;
        }
    }
}
