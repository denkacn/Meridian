using System;
using System.Collections.Generic;
using MeridianRequestSystem.RequestSystem.Utilities;

namespace MeridianRequestSystem.RequestSystem.Interfaces
{
    public interface INetworkBus
    {
        void Init(INetworkSender sender, ILogger logWriter);
        void SendRequest<TRes>(IDataNetworkRequest request, Action<TRes> responseCallback) where TRes : IDataNetworkResponse;
        void IncomingResponse(Dictionary<byte, object> responseData);
        string SubscribeToStaticResponse<TRes>(int responseType, Action<TRes> responseCallback) where TRes : IDataNetworkResponse;
        void UnsubscribeFromStaticResponse(int responseType, string id);
    }
}
