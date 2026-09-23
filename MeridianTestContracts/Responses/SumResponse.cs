using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Responses
{
    public class SumResponse : BaseDataResponse
    {
        [RequestField(10)]
        public int Result;

        public SumResponse()
            : base((int)TestRequestType.Sum)
        {
        }

        public SumResponse(BaseDataRequest request, int result)
            : base(request, 0)
        {
            Result = result;
        }
    }
}
