using System;
using System.Collections.Generic;
using MeridianRequestSystem.RequestSystem.Requests;

namespace MeridianTestContracts.Responses
{
    internal static class ResponseMap
    {
        public static void ReadBase(BaseDataResponse response, IReadOnlyDictionary<byte, object> package)
        {
            response.RequestId = ReadString(package, 1);
            response.ReturnCode = ReadInt(package, 99);
        }

        public static string ReadString(IReadOnlyDictionary<byte, object> package, byte key)
        {
            return package.TryGetValue(key, out var value) ? value as string : null;
        }

        public static int ReadInt(IReadOnlyDictionary<byte, object> package, byte key)
        {
            return package.TryGetValue(key, out var value) ? Convert.ToInt32(value) : 0;
        }
    }
}
