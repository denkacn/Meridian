using System;
using System.Collections.Generic;
using MeridianServerLib.Interfaces.Operations;

namespace MeridianServerLib.Models.Operations
{
    [Serializable]
    public readonly struct OperationData : IOperationData
    {
        public byte OperationCode { get; }
        public Dictionary<byte, object> Parameters { get; }

        public OperationData(byte operationCode)
        {
            OperationCode = operationCode;
            Parameters = new Dictionary<byte, object>();
        }

        public OperationData(byte operationCode, Dictionary<byte, object> parameters)
        {
            OperationCode = operationCode;
            Parameters = parameters;
        }
    }
}