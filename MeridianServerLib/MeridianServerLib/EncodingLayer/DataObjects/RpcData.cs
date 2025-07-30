using System;
using System.Collections.Generic;

namespace MeridianServerLib.EncodingLayer.DataObjects
{
    [Serializable]
    public class RpcData
    {
        public byte C { get; set; }
        public Dictionary<byte, object> P { get; set; } = new Dictionary<byte, object>();

        public RpcData() { }

        public RpcData(byte code, Dictionary<byte, object> param)
        {
            C = code;
            P = param;
        }

        public void Add(byte key, object value)
        {
            if (!P.ContainsKey(key))
            {
                P.Add(key, value);
            }
        }
    }
}