using MeridianServerLib.EncodingLayer.DataObjects;
using MeridianServerLib.Models.Operations;

namespace MeridianServerLib.EncodingLayer.Convertors
{
    public class RpcDataConvertor
    {
        public RpcData To(OperationData data)
        {
            return new RpcData(data.OperationCode, data.Parameters);
        }

        public OperationData From(RpcData data)
        {
            return new OperationData(data.C, data.P);
        }
    }
}