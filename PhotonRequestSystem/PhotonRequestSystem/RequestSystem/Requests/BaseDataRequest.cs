using System.Collections.Generic;
using PhotonRequestSystem.RequestSystem.Attributes;
using PhotonRequestSystem.RequestSystem.Interfaces;
using PhotonRequestSystem.RequestSystem.Utilities;

namespace PhotonRequestSystem.RequestSystem.Requests
{
    public class BaseDataRequest : IDataNetworkRequest
    {
        [RequestField(1)]
        public string RequestId;

        [RequestField(2)]
        public readonly int RequestType;

        [RequestField(99)]
        public readonly int RequestCode = 99;

        public BaseDataRequest() { }

        public BaseDataRequest(int requestType)
        {
            RequestType = requestType;
            RequestCode = GetRealHashCode.FromString(GetType().Name);
        }

        public void SetRequestId(string requestId)
        {
            RequestId = requestId;
        }
    }

    public class BaseDataResponse : IDataNetworkResponse
    {
        [RequestField(1)]
        public string RequestId;

        [RequestField(2)]
        public readonly int RequestType;

        [RequestField(99)]
        public int ReturnCode = 99;

        public BaseDataResponse() { }

        public BaseDataResponse(BaseDataRequest request, int returnCode)
        {
            RequestId = request.RequestId;
            RequestType = request.RequestType;
            ReturnCode = returnCode;
        }

        public BaseDataResponse(int requestType)
        {
            RequestId = "1111";
            RequestType = requestType;
            ReturnCode = 99;
        }

        public virtual void Map(Dictionary<byte, object> package) { }
    }
}
