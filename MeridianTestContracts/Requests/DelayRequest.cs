using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Requests
{
    [RequestBase(TestOperationCodes.RequestSystem, true)]
    public class DelayRequest : BaseDataRequest
    {
        [RequestField(10)]
        public int DelayMilliseconds;

        public DelayRequest() : base((int)TestRequestType.Delay) { }

        public DelayRequest(int delayMilliseconds) : this() => DelayMilliseconds = delayMilliseconds;
    }
}
