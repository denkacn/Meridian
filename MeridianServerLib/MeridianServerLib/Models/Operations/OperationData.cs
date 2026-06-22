using System;
using System.Collections.Generic;
using MeridianServerLib.Interfaces.Operations;
using MessagePack;

namespace MeridianServerLib.Models.Operations
{
    [Serializable]
    [MessagePackObject]
    public readonly struct OperationData : IOperationData
    {
        [Key(0)]
        public byte OperationCode { get; }

        [Key(1)]
        public Dictionary<byte, object> Parameters { get; }

        public OperationData(byte operationCode)
        {
            OperationCode = operationCode;
            Parameters = new Dictionary<byte, object>();
        }

        [SerializationConstructor]
        public OperationData(byte operationCode, Dictionary<byte, object> parameters)
        {
            OperationCode = operationCode;
            Parameters = parameters;
        }
    }
}
