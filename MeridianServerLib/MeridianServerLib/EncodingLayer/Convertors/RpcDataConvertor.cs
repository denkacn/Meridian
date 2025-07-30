using MeridianServerLib.EncodingLayer.DataObjects;
using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.EncodingLayer.Convertors
{
    public class RpcDataConvertor
    {
        public RpcData To(OperationData data)
        {
            var packData = new RpcData(data.OperationCode, data.Parameters);

            return packData;
        }

        public OperationData From(RpcData data)
        {
            var operationData = new OperationData(data.C, data.P);

            return operationData;
        }
    }
}