using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Requests
{
    [RequestBase(TestOperationCodes.RequestSystem, true)]
    public class SumRequest : BaseDataRequest
    {
        [RequestField(10)]
        public int A;

        [RequestField(11)]
        public int B;

        public SumRequest()
            : base((int)TestRequestType.Sum)
        {
        }

        public SumRequest(int a, int b)
            : this()
        {
            A = a;
            B = b;
        }
    }
}
