using MessagePack;
using System.Collections.Generic;

namespace MeridianServerLib.EncodingLayer.DataObjects
{
	[MessagePackObject]
	public readonly struct RpcData
    {
		[Key(0)] public byte C { get; }
		[Key(1)] public Dictionary<byte, object> P { get; }

        public RpcData(byte code, Dictionary<byte, object> param)
        {
            C = code;
            P = param;
        }

        public void Add(byte key, object value)
        {
	        P.TryAdd(key, value);
        }
    }
}