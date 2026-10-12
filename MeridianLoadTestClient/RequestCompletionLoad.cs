using System.Diagnostics;
using System.Reflection;
using MeridianRequestSystem.RequestSystem.Bus;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianTestContracts.Requests;
using MeridianTestContracts.Responses;

namespace MeridianLoadTestClient;

internal static class RequestCompletionLoad
{
    public static async Task<int> RunAsync(LoadOptions options)
    {
        ThreadPool.GetMinThreads(out var minWorkers, out var minIo);
        ThreadPool.SetMinThreads(Math.Max(minWorkers, options.Clients * 2 + 4), minIo);
        var clients = Enumerable.Range(0, options.Clients)
            .Select(_ => new LoadClient(options.Host, options.Port)).ToArray();
        try
        {
            using var connectTimeout = new CancellationTokenSource(options.ConnectTimeout);
            var connected = await Task.WhenAll(clients.Select(client => client.ConnectAsync(connectTimeout.Token)));
            if (connected.Any(value => !value)) throw new IOException("A load client did not connect.");

            Console.WriteLine($"Completion load: {options.Clients} clients, {options.Duration.TotalSeconds:N0}s");
            using var duration = new CancellationTokenSource(options.Duration);
            var completed = 0L;
            var stopwatch = Stopwatch.StartNew();
            await Task.WhenAll(clients.Select(async client =>
            {
                while (!duration.IsCancellationRequested)
                {
                    await CheckTransportContinuationAsync(client, options.Timeout);
                    Interlocked.Increment(ref completed);
                }
            }));
            stopwatch.Stop();
            Console.WriteLine($"Transport checks: {completed:N0}; rate: {completed / stopwatch.Elapsed.TotalSeconds:N1}/s; stalled responses: 0");

            var lifecycle = await RunLifecycleStressAsync();
            Console.WriteLine($"Lifecycle: {lifecycle:N0} pending requests failed on disconnect; rejected before Init; accepted after Init; stale responses ignored");
            Console.WriteLine("PASS: request completion load");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: request completion load");
            Console.Error.WriteLine(exception);
            return 2;
        }
        finally
        {
            foreach (var client in clients) client.Dispose();
        }
    }

    private static async Task CheckTransportContinuationAsync(LoadClient client, TimeSpan timeout)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = client.SendAsync<DelayResponse>(new DelayRequest(40), timeout, CancellationToken.None);
        var continuation = first.ContinueWith(_ =>
        {
            entered.TrySetResult(true);
            if (!release.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Continuation release timed out.");
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        try
        {
            await entered.Task.WaitAsync(timeout);
            var second = await client.SendAsync<PingResponse>(new PingRequest(), timeout, CancellationToken.None)
                .WaitAsync(timeout);
            if (second.Message == null) throw new InvalidDataException("Ping response was not mapped.");
        }
        finally
        {
            release.Set();
            await continuation.WaitAsync(TimeSpan.FromSeconds(5));
            await first;
        }
    }

    private static async Task<long> RunLifecycleStressAsync()
    {
        const int cycles = 100;
        const int pendingPerCycle = 256;
        var sender = new CountingSender();
        var bus = new DataServerNetworkBus();
        bus.Init(sender);
        long failed = 0;

        for (var cycle = 0; cycle < cycles; cycle++)
        {
            var requests = Enumerable.Range(0, pendingPerCycle).Select(_ => new PingRequest()).ToArray();
            var pending = requests.Select(request => bus.SendRequestAsync<PingResponse>(request)).ToArray();
            bus.CancelPendingRequests();
            foreach (var task in pending)
            {
                try
                {
                    await task.WaitAsync(TimeSpan.FromSeconds(5));
                    throw new InvalidDataException("Pending request succeeded after disconnect.");
                }
                catch (IOException)
                {
                    failed++;
                }
            }

            if (PendingCount(bus) != 0) throw new InvalidDataException("Pending requests leaked after disconnect.");
            var sends = sender.Count;
            var rejectedRequest = new PingRequest();
            try
            {
                await bus.SendRequestAsync<PingResponse>(rejectedRequest);
                throw new InvalidDataException("Request was accepted before Init.");
            }
            catch (IOException) { }
            if (sender.Count != sends || rejectedRequest.RequestId != null)
                throw new InvalidDataException("Rejected request reached the sender.");

            bus.Init(sender);
            var freshRequest = new PingRequest();
            var fresh = bus.SendRequestAsync<PingResponse>(freshRequest);
            bus.IncomingResponse(Response(requests[0].RequestId));
            if (fresh.IsCompleted) throw new InvalidDataException("Stale response completed a new request.");
            bus.IncomingResponse(Response(freshRequest.RequestId));
            await fresh.WaitAsync(TimeSpan.FromSeconds(5));
            if (PendingCount(bus) != 0) throw new InvalidDataException("Completed request retained a pending entry.");
        }

        return failed;
    }

    private static Dictionary<byte, object> Response(string requestId) => new()
    {
        [1] = requestId,
        [2] = 0,
        [10] = "ok",
        [99] = 0
    };

    private static int PendingCount(DataServerNetworkBus bus)
    {
        var field = typeof(DataServerNetworkBus).GetField("_pendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("_pendingRequests");
        var pending = field.GetValue(bus) ?? throw new InvalidDataException("Pending map is null.");
        return (int)(pending.GetType().GetProperty("Count")?.GetValue(pending)
            ?? throw new MissingMemberException("Pending map count"));
    }

    private sealed class CountingSender : INetworkSender
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);

        public void Send(byte code, Dictionary<byte, object> package, bool isNecessarily) =>
            Interlocked.Increment(ref _count);
    }
}
