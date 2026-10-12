using MeridianRequestSystem.RequestSystem.Helpers;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianRequestSystem.RequestSystem.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MeridianRequestSystem.RequestSystem.Bus
{
    public class DataServerNetworkBus : INetworkBus
    {
        private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
        private static long _requestId;

        private INetworkSender _networkSender;
        private ConcurrentDictionary<string, ResponseCallbackData> _responseCallbacksMap;
        private ConcurrentDictionary<string, IPendingRequest> _pendingRequests;
        private ConcurrentDictionary<int, List<ResponseCallbackData>> _staticResponseCallbacksMap;
        private ILogger _logWriter;
        private readonly object _lifecycleGate = new object();
        private bool _acceptingRequests;

        public void Init(INetworkSender sender, ILogger logWriter = null)
        {
            ConcurrentDictionary<string, IPendingRequest> previous;
            lock (_lifecycleGate)
            {
                previous = _pendingRequests;
                _responseCallbacksMap = new ConcurrentDictionary<string, ResponseCallbackData>();
                _pendingRequests = new ConcurrentDictionary<string, IPendingRequest>();
                _staticResponseCallbacksMap = new ConcurrentDictionary<int, List<ResponseCallbackData>>();
                _networkSender = sender;
                _logWriter = logWriter;
                _acceptingRequests = true;
            }

            Drain(previous);

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
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<TRes>(cancellationToken);

            PendingRequest<TRes> pending = null;
            INetworkSender sender;
            try
            {
                lock (_lifecycleGate)
                {
                    var callbacks = _pendingRequests ?? throw new InvalidOperationException("Network bus is not initialized.");
                    if (!_acceptingRequests)
                        throw new IOException("Connection closed before the request could be sent.");
                    sender = _networkSender ?? throw new InvalidOperationException("Network sender is not initialized.");
                    var id = CreateUniqId();
                    request.SetRequestId(id);
                    pending = new PendingRequest<TRes>(id, callbacks, cancellationToken);
                    callbacks[id] = pending;
                }

                pending.Initialize(timeout ?? DefaultRequestTimeout);
                if (pending.IsPending)
                {
                    var requestData = RequestMapper.GetRequestData(request);
                    if (pending.IsPending)
                        sender.Send(requestData.CommandId, requestData.Request, requestData.IsNecessarily);
                }
            }
            catch (Exception exception)
            {
                if (pending == null)
                    return Task.FromException<TRes>(exception);
                pending.TryFail(exception);
            }

            return pending.Task;
        }

        public void CancelPendingRequests()
        {
            ConcurrentDictionary<string, IPendingRequest> pending;
            lock (_lifecycleGate)
            {
                _acceptingRequests = false;
                pending = _pendingRequests;
            }
            Drain(pending);
        }

        private static void Drain(ConcurrentDictionary<string, IPendingRequest> pending)
        {
            if (pending == null) return;
            foreach (var request in pending.Values)
                request.TryFail(new IOException("Connection closed before the request completed."));
        }

        public void IncomingResponse(Dictionary<byte, object> responseData)
        {
            if (responseData == null) return;
            if (_logWriter != null)
            {
                foreach (var pair in responseData)
                {
                    _logWriter.WriteLog($"[MeridianRequestSystem] responseData key: {pair.Key} value: {pair.Value}");
                }
            }

            if (!responseData.ContainsKey(99)) return;
            if (!responseData.TryGetValue(1, out var requestIdValue)) return;
            if (!responseData.TryGetValue(2, out var responseTypeValue)) return;

            var id = requestIdValue as string;
            if (string.IsNullOrEmpty(id)) return;

            if (_pendingRequests != null && _pendingRequests.TryGetValue(id, out var pending))
                pending.TryCompleteResponse(responseData);
            else if (_responseCallbacksMap.TryRemove(id, out var responseCallback))
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
            return Interlocked.Increment(ref _requestId).ToString(CultureInfo.InvariantCulture);
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

        private interface IPendingRequest
        {
            void TryCompleteResponse(Dictionary<byte, object> responseData);
            void TryFail(Exception exception);
        }

        private sealed class PendingRequest<TRes> : IPendingRequest where TRes : IDataNetworkResponse
        {
            private readonly string _id;
            private readonly ConcurrentDictionary<string, IPendingRequest> _owner;
            private readonly CancellationToken _cancellationToken;
            private readonly TaskCompletionSource<TRes> _completion =
                new TaskCompletionSource<TRes>(TaskCreationOptions.RunContinuationsAsynchronously);
            private CancellationTokenRegistration _cancellationRegistration;
            private Timer _timeoutTimer;
            private int _terminal;
            private int _initialized;
            private int _cleanupScheduled;

            public PendingRequest(string id, ConcurrentDictionary<string, IPendingRequest> owner,
                CancellationToken cancellationToken)
            {
                _id = id;
                _owner = owner;
                _cancellationToken = cancellationToken;
            }

            public Task<TRes> Task => _completion.Task;
            public bool IsPending => Volatile.Read(ref _terminal) == 0;

            public void Initialize(TimeSpan timeout)
            {
                try
                {
                    if (_cancellationToken.CanBeCanceled)
                        _cancellationRegistration = _cancellationToken.Register(() => TryCancel());

                    if (IsPending)
                        _timeoutTimer = new Timer(_ => TryTimeout(), null, timeout, Timeout.InfiniteTimeSpan);
                }
                finally
                {
                    Volatile.Write(ref _initialized, 1);
                    ScheduleCleanup();
                }
            }

            public void TryCompleteResponse(Dictionary<byte, object> responseData)
            {
                if (Interlocked.CompareExchange(ref _terminal, 1, 0) != 0) return;

                try
                {
                    var response = (TRes)Activator.CreateInstance(typeof(TRes));
                    response.Map(responseData);
                    Finish(() => _completion.TrySetResult(response));
                }
                catch (Exception exception)
                {
                    Finish(() => _completion.TrySetException(exception));
                }
            }

            public void TryFail(Exception exception)
            {
                if (Interlocked.CompareExchange(ref _terminal, 1, 0) != 0) return;
                Finish(() => _completion.TrySetException(exception));
            }

            private void TryCancel()
            {
                if (Interlocked.CompareExchange(ref _terminal, 1, 0) != 0) return;
                Finish(() => _completion.TrySetCanceled(_cancellationToken));
            }

            private void TryTimeout()
            {
                TryFail(new TimeoutException("Request timeout: " + _id));
            }

            private void Finish(Action complete)
            {
                ((ICollection<KeyValuePair<string, IPendingRequest>>)_owner)
                    .Remove(new KeyValuePair<string, IPendingRequest>(_id, this));
                try
                {
                    complete();
                }
                finally
                {
                    ScheduleCleanup();
                }
            }

            private void ScheduleCleanup()
            {
                if (Volatile.Read(ref _terminal) == 0 || Volatile.Read(ref _initialized) == 0 ||
                    Interlocked.Exchange(ref _cleanupScheduled, 1) != 0)
                    return;

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    _cancellationRegistration.Dispose();
                    _timeoutTimer?.Dispose();
                });
            }
        }
    }
}
