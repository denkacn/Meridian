using System;
using System.Collections.Generic;
using MeridianServerLib.Interfaces.Operations;

namespace MeridianServerLib.Models.Operations
{
    [Serializable]
    public class OperationData : IOperationData
    {
        public byte OperationCode { get; set; }
        public string DebugMessage { get; set; }
        public short ReturnCode { get; set; }
        public Dictionary<byte, object> Parameters { get; set; }
        
        public OperationData()
        {
        }

        public OperationData(byte operationCode)
        {
            OperationCode = operationCode;
        }

        public OperationData(byte operationCode, Dictionary<byte, object> parameters)
        {
            OperationCode = operationCode;
            Parameters = parameters;
        }
    }
}