using MeridianRequestSystem.RequestSystem.Bus;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianTestContracts.Requests;
using MeridianTestContracts.Responses;

namespace MeridianTestClient;

internal static class RequestCompletionChecks
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public static async Task<int> RunAsync()
    {
        try
        {
            await RunWithDeadline(AlreadyCanceledAsync());
            await RunWithDeadline(CanceledDuringRegistrationAsync());
            await RunWithDeadline(ImmediateResponseAsync());
            await RunWithDeadline(CancelDuringMapAsync());
            await RunWithDeadline(TimeoutAndLateResponseAsync());
            await RunWithDeadline(FailuresAsync());
            await RunWithDeadline(DisconnectAndReinitializeAsync());
            await RunWithDeadline(DisconnectDuringSendAsync());
            await RunWithDeadline(ContinuationCanDisconnectAsync());
            await RunWithDeadline(RepeatedLifecycleAsync());
            Console.WriteLine("Request completion checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task AlreadyCanceledAsync()
    {
        var sender = new FakeSender();
        var bus = NewBus(sender);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = bus.SendRequestAsync<PingResponse>(new PingRequest(), cancellationToken: cancellation.Token);
        await ExpectAsync<OperationCanceledException>(result);
        Check(sender.SendCount == 0, "Already-canceled request was sent.");
    }

    private static async Task ImmediateResponseAsync()
    {
        var sender = new FakeSender();
        var bus = NewBus(sender);
        sender.OnSend = package => bus.IncomingResponse(Response(package));
        var request = new PingRequest();
        var result = await bus.SendRequestAsync<PingResponse>(request);
        Check(result.Message == "ok", "Immediate response was not mapped.");
        bus.IncomingResponse(Response(request.RequestId));
    }

    private static async Task CanceledDuringRegistrationAsync()
    {
        var sender = new FakeSender();
        var bus = NewBus(sender);
        using var cancellation = new CancellationTokenSource();
        var request = new CancelOnIdRequest(cancellation);
        await ExpectAsync<OperationCanceledException>(
            bus.SendRequestAsync<PingResponse>(request, cancellationToken: cancellation.Token));
        Check(sender.SendCount == 0, "Canceled request was sent during registration.");
    }

    private static async Task CancelDuringMapAsync()
    {
        var sender = new FakeSender();
        var bus = NewBus(sender);
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        BlockingResponse.Entered = entered;
        BlockingResponse.Release = release;
        try
        {
            var request = new PingRequest();
            var result = bus.SendRequestAsync<BlockingResponse>(request, cancellationToken: cancellation.Token);
            var receiving = Task.Run(() => bus.IncomingResponse(Response(request.RequestId)));
            Check(entered.Wait(Deadline), "Response Map did not start.");
            cancellation.Cancel();
            release.Set();
            await receiving;
            await result;
            bus.IncomingResponse(Response(request.RequestId));
        }
        finally
        {
            release.Set();
            BlockingResponse.Entered = null;
            BlockingResponse.Release = null;
        }
    }

    private static async Task TimeoutAndLateResponseAsync()
    {
        var bus = NewBus(new FakeSender());
        var request = new PingRequest();
        var result = bus.SendRequestAsync<PingResponse>(request, TimeSpan.FromMilliseconds(1));
        await ExpectAsync<TimeoutException>(result);
        bus.IncomingResponse(Response(request.RequestId));
        await ExpectAsync<TimeoutException>(result);

        using var cancellation = new CancellationTokenSource();
        var canceledRequest = new PingRequest();
        var canceled = bus.SendRequestAsync<PingResponse>(canceledRequest, cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await ExpectAsync<OperationCanceledException>(canceled);
        bus.IncomingResponse(Response(canceledRequest.RequestId));
    }

    private static async Task FailuresAsync()
    {
        var sender = new FakeSender { OnSend = _ => throw new IOException("send failed") };
        var bus = NewBus(sender);
        await ExpectAsync<IOException>(bus.SendRequestAsync<PingResponse>(new PingRequest()));

        sender.OnSend = null;
        await ExpectAsync<Exception>(bus.SendRequestAsync<PingResponse>(new MissingAttributeRequest()));

        var request = new PingRequest();
        var result = bus.SendRequestAsync<ThrowingResponse>(request);
        bus.IncomingResponse(Response(request.RequestId));
        await ExpectAsync<InvalidDataException>(result);
    }

    private static async Task DisconnectAndReinitializeAsync()
    {
        var bus = NewBus(new FakeSender());
        var oldRequest = new PingRequest();
        var old = bus.SendRequestAsync<PingResponse>(oldRequest);
        var others = Enumerable.Range(0, 32)
            .Select(_ => bus.SendRequestAsync<PingResponse>(new PingRequest())).ToArray();
        bus.CancelPendingRequests();
        await ExpectAsync<IOException>(old);
        foreach (var result in others)
            await ExpectAsync<IOException>(result);

        var replacement = new FakeSender();
        bus.Init(replacement);
        var freshRequest = new PingRequest();
        var fresh = bus.SendRequestAsync<PingResponse>(freshRequest);
        bus.IncomingResponse(Response(oldRequest.RequestId));
        Check(!fresh.IsCompleted, "Old response completed a replacement request.");
        bus.IncomingResponse(Response(freshRequest.RequestId));
        await fresh;
    }

    private static async Task DisconnectDuringSendAsync()
    {
        var sender = new FakeSender();
        var bus = NewBus(sender);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        sender.OnSend = _ =>
        {
            entered.Set();
            if (!release.Wait(Deadline)) throw new TimeoutException("Send barrier timed out.");
        };

        try
        {
            var sending = Task.Run(() => bus.SendRequestAsync<PingResponse>(new PingRequest()));
            Check(entered.Wait(Deadline), "Send did not start.");
            bus.CancelPendingRequests();
            await ExpectAsync<IOException>(bus.SendRequestAsync<PingResponse>(new PingRequest()));
            release.Set();
            await ExpectAsync<IOException>(sending);

            bus.Init(new FakeSender());
            var replacement = new PingRequest();
            var response = bus.SendRequestAsync<PingResponse>(replacement);
            bus.IncomingResponse(Response(replacement.RequestId));
            await response;
        }
        finally
        {
            release.Set();
        }
    }

    private static async Task ContinuationCanDisconnectAsync()
    {
        var bus = NewBus(new FakeSender());
        using var transportGate = new SemaphoreSlim(1, 1);
        var request = new PingRequest();
        var response = bus.SendRequestAsync<PingResponse>(request);
        var continuation = response.ContinueWith(_ =>
        {
            transportGate.Wait();
            try
            {
                bus.CancelPendingRequests();
            }
            finally
            {
                transportGate.Release();
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        var receiving = Task.Run(() =>
        {
            transportGate.Wait();
            try
            {
                bus.IncomingResponse(Response(request.RequestId));
            }
            finally
            {
                transportGate.Release();
            }
        });

        await receiving.WaitAsync(Deadline);
        await continuation.WaitAsync(Deadline);
        await response;
    }

    private static async Task RepeatedLifecycleAsync()
    {
        var bus = NewBus(new FakeSender());
        for (var cycle = 0; cycle < 100; cycle++)
        {
            using var cancellation = new CancellationTokenSource();
            var request = new PingRequest();
            var response = bus.SendRequestAsync<PingResponse>(request, cancellationToken: cancellation.Token);
            cancellation.Cancel();
            await ExpectAsync<OperationCanceledException>(response);
            Check(PendingCount(bus) == 0, "Completed request retained a pending entry.");
            bus.CancelPendingRequests();
            bus.Init(new FakeSender());
        }
    }

    private static int PendingCount(DataServerNetworkBus bus)
    {
        var field = typeof(DataServerNetworkBus).GetField("_pendingRequests",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var pending = field.GetValue(bus);
        return (int)pending.GetType().GetProperty("Count").GetValue(pending);
    }

    private static DataServerNetworkBus NewBus(FakeSender sender)
    {
        var bus = new DataServerNetworkBus();
        bus.Init(sender);
        return bus;
    }

    private static Dictionary<byte, object> Response(Dictionary<byte, object> request) =>
        Response((string)request[1]);

    private static Dictionary<byte, object> Response(string requestId) => new()
    {
        [1] = requestId,
        [2] = 0,
        [10] = "ok",
        [99] = 0
    };

    private static async Task ExpectAsync<TException>(Task task) where TException : Exception
    {
        try
        {
            await task;
        }
        catch (TException)
        {
            return;
        }

        throw new Exception("Expected " + typeof(TException).Name);
    }

    private static Task RunWithDeadline(Task task) => task.WaitAsync(Deadline);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class FakeSender : INetworkSender
    {
        public Action<Dictionary<byte, object>> OnSend;
        public int SendCount;

        public void Send(byte code, Dictionary<byte, object> package, bool isNecessarily)
        {
            Interlocked.Increment(ref SendCount);
            OnSend?.Invoke(package);
        }
    }

    private sealed class MissingAttributeRequest : IDataNetworkRequest
    {
        public void SetRequestId(string requestId) { }
    }

    private sealed class CancelOnIdRequest : IDataNetworkRequest
    {
        private readonly CancellationTokenSource _cancellation;

        public CancelOnIdRequest(CancellationTokenSource cancellation) => _cancellation = cancellation;

        public void SetRequestId(string requestId) => _cancellation.Cancel();
    }

    public sealed class BlockingResponse : IDataNetworkResponse
    {
        public static ManualResetEventSlim Entered;
        public static ManualResetEventSlim Release;

        public void Map(Dictionary<byte, object> package)
        {
            Entered.Set();
            if (!Release.Wait(Deadline)) throw new TimeoutException("Map barrier timed out.");
        }
    }

    public sealed class ThrowingResponse : IDataNetworkResponse
    {
        public void Map(Dictionary<byte, object> package) => throw new InvalidDataException("map failed");
    }
}
