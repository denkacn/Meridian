using MeridianRequestSystem.RequestSystem.Helpers;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianRequestSystem.RequestSystem.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MeridianRequestSystem.RequestSystem.Bus
{
    public class DataServerNetworkBus : INetworkBus
    {
        private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

        private INetworkSender _networkSender;
        private ConcurrentDictionary<string, ResponseCallbackData> _responseCallbacksMap;
        private ConcurrentDictionary<int, List<ResponseCallbackData>> _staticResponseCallbacksMap;
        private ILogger _logWriter;

        public void Init(INetworkSender sender, ILogger logWriter = null)
        {
            _responseCallbacksMap = new ConcurrentDictionary<string, ResponseCallbackData>();
            _staticResponseCallbacksMap = new ConcurrentDictionary<int, List<ResponseCallbackData>>();
            _networkSender = sender;
            _logWriter = logWriter;

            if (_logWriter != null)
            {
                _logWriter.WriteLog("[MeridianRequestSystem] DataServerNetworkBus Init");
            }
        }

        public void SendRequest<TRes>(IDataNetworkRequest request, Action<TRes> responseCallback) where TRes : IDataNetworkResponse
        {
            var uId = CreateUniqId();
            request.SetRequestId(uId);

            if (_logWriter != null)
            {
                _logWriter.WriteLog("[MeridianRequestSystem] SetRequestId: " + uId);
            }

            var convertObject = Convert(responseCallback);
            var callbackData = new ResponseCallbackData(convertObject, uId);
            callbackData.CreateResponse(typeof(TRes));

            _responseCallbacksMap[uId] = callbackData;

            var requestData = RequestMapper.GetRequestData(request);
            _networkSender.Send(requestData.CommandId, requestData.Request, requestData.IsNecessarily);
        }

        public Task<TRes> SendRequestAsync<TRes>(
            IDataNetworkRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            where TRes : IDataNetworkResponse
        {
            var completionSource = new TaskCompletionSource<TRes>();
            CancellationTokenSource timeoutSource = null;
            CancellationTokenRegistration cancellationRegistration = default;

            void Cleanup(string requestId)
            {
                _responseCallbacksMap.TryRemove(requestId, out _);
                timeoutSource?.Dispose();
                cancellationRegistration.Dispose();
            }

            var uId = CreateUniqId();
            request.SetRequestId(uId);

            var callbackData = new ResponseCallbackData(response =>
            {
                Cleanup(uId);
                completionSource.TrySetResult((TRes)response);
            }, uId);
            callbackData.CreateResponse(typeof(TRes));
            _responseCallbacksMap[uId] = callbackData;

            timeoutSource = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
            timeoutSource.Token.Register(() =>
            {
                Cleanup(uId);
                completionSource.TrySetException(new TimeoutException("Request timeout: " + uId));
            });

            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(() =>
                {
                    Cleanup(uId);
                    completionSource.TrySetCanceled(cancellationToken);
                });
            }

            var requestData = RequestMapper.GetRequestData(request);
            _networkSender.Send(requestData.CommandId, requestData.Request, requestData.IsNecessarily);

            return completionSource.Task;
        }

        public void IncomingResponse(Dictionary<byte, object> responseData)
        {
            if (_logWriter != null)
            {
                foreach (var pair in responseData)
                {
                    _logWriter.WriteLog($"[MeridianRequestSystem] responseData key: {pair.Key} value: {pair.Value}");
                }
            }

            if (responseData == null) return;
            if (!responseData.ContainsKey(99)) return;
            if (!responseData.TryGetValue(1, out var requestIdValue)) return;
            if (!responseData.TryGetValue(2, out var responseTypeValue)) return;

            var id = requestIdValue as string;
            if (string.IsNullOrEmpty(id)) return;

            if (_responseCallbacksMap.TryRemove(id, out var responseCallback))
            {
                responseCallback.Response.Map(responseData);
                responseCallback.RunCallback();
            }

            var responseType = System.Convert.ToInt32(responseTypeValue);

            if (_logWriter != null)
            {
                _logWriter.WriteLog($"[MeridianRequestSystem] IncomingResponse id: {id} responseType: {responseType}");
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

            var responses = _staticResponseCallbacksMap.GetOrAdd(responseType, _ => new List<ResponseCallbackData>());
            lock (responses)
            {
                responses.Add(callbackData);
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
