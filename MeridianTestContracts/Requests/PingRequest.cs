using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Requests
{
    [RequestBase(TestOperationCodes.RequestSystem, true)]
    public class PingRequest : BaseDataRequest
    {
        public PingRequest()
            : base((int)TestRequestType.Ping)
        {
        }
    }
}
