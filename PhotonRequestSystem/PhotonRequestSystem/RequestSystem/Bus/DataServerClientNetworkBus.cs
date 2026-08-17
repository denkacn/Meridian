using PhotonRequestSystem.RequestSystem.Helpers;
using PhotonRequestSystem.RequestSystem.Interfaces;
using PhotonRequestSystem.RequestSystem.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PhotonRequestSystem.RequestSystem.Bus
{
    public class DataServerNetworkBus : INetworkBus
    {
        private INetworkSender _networkSender;
        private Dictionary<string, ResponseCallbackData> _responseCallbacksMap;
        private Dictionary<int, List<ResponseCallbackData>> _staticResponseCallbacksMap;
        private ILogger _logWriter;

        public void Init(INetworkSender sender, ILogger logWriter = null)
        {
            _responseCallbacksMap = new Dictionary<string, ResponseCallbackData>();
            _staticResponseCallbacksMap = new Dictionary<int, List<ResponseCallbackData>>();
            _networkSender = sender;
            _logWriter = logWriter;

            if (_logWriter != null)
            {
                _logWriter.WriteLog("[PhotonRequestSystem] DataServerNetworkBus Init");
            }
        }

        public void SendRequest<TRes>(IDataNetworkRequest request, Action<TRes> responseCallback) where TRes : IDataNetworkResponse
        {
            var uId = CreateUniqId();
            request.SetRequestId(uId);

            if (_logWriter != null)
            {
                _logWriter.WriteLog("[PhotonRequestSystem] SetRequestId: " + uId);
            }

            var convertObject = Convert(responseCallback);
            var callbackData = new ResponseCallbackData(convertObject, uId);
            callbackData.CreateResponse(typeof(TRes));

            _responseCallbacksMap.Add(uId, callbackData);

            var requestData = RequestMapper.GetRequestData(request);
            _networkSender.Send(requestData.CommandId, requestData.Request, requestData.IsNecessarily);
        }

        public void IncomingResponse(Dictionary<byte, object> responseData)
        {
            if (_logWriter != null)
            {
                foreach (var pair in responseData)
                {
                    _logWriter.WriteLog($"[PhotonRequestSystem] responseData key: {pair.Key} value: {pair.Value}");
                }
            }

            if (!responseData.ContainsKey(99)) return;

            var id = (string) responseData[1];

            if (_responseCallbacksMap.ContainsKey(id))
            {
                var responseCallback = _responseCallbacksMap[id];
                responseCallback.Response.Map(responseData);
                responseCallback.RunCallback();

                _responseCallbacksMap.Remove(id);
            }

            var responseType = (int) responseData[2];

            if (_logWriter != null)
            {
                _logWriter.WriteLog($"[PhotonRequestSystem] IncomingResponse id: {id} responseType: {responseType}");
            }

            if (_staticResponseCallbacksMap.ContainsKey(responseType))
            {
                var runCallbacks = new List<ResponseCallbackData>(); 
                foreach (var response in _staticResponseCallbacksMap[responseType])
                {
                    response.Response.Map(responseData);
                    runCallbacks.Add(response);
                }

                runCallbacks.ForEach(c => c.RunCallback());
            }
        }

        public string SubscribeToStaticResponse<TRes>(int responseType, Action<TRes> responseCallback) where TRes : IDataNetworkResponse
        {
            var id = CreateUniqId();

            var convertObject = Convert(responseCallback);
            var callbackData = new ResponseCallbackData(convertObject, id);
            callbackData.CreateResponse(typeof(TRes));

            if (_staticResponseCallbacksMap.ContainsKey(responseType))
            {
                _staticResponseCallbacksMap[responseType].Add(callbackData);
            }
            else
            {
                _staticResponseCallbacksMap.Add(responseType, new List<ResponseCallbackData>() {callbackData});
            }

            return id;
        }

        public void UnsubscribeFromStaticResponse(int responseType, string id) 
        {
            if (_staticResponseCallbacksMap.ContainsKey(responseType))
            {
                _staticResponseCallbacksMap[responseType].RemoveAll(r => r.Id == id);
            }
        }

        private Action<object> Convert<T>(Action<T> myActionT)
        {
            if (myActionT == null) return null;
            return o => myActionT((T)o);
        }

        private string CreateUniqId()
        {
            var builder = new StringBuilder();
            Enumerable
               .Range(65, 26)
                .Select(e => ((char)e).ToString())
                .Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString()))
                .Concat(Enumerable.Range(0, 10).Select(e => e.ToString()))
                .OrderBy(e => Guid.NewGuid())
                .Take(5)
                .ToList().ForEach(e => builder.Append(e));

            return builder.ToString();
        }

        private class ResponseCallbackData
        {
            private readonly Action<IDataNetworkResponse> _callback;
            public IDataNetworkResponse Response;
            public readonly string Id;

            public ResponseCallbackData(Action<IDataNetworkResponse> callback, string id)
            {
                _callback = callback;
                Id = id;
            }

            public void CreateResponse(Type responseType)
            {
                Response = (IDataNetworkResponse) Activator.CreateInstance(responseType);
            }

            public void RunCallback()
            {
                if (_callback != null) _callback(Response);
            }
        }
    }
}
