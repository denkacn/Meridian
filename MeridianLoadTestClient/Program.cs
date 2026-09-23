using System.Collections.Concurrent;
using System.Diagnostics;
using MeridianRequestSystem.RequestSystem.Bus;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianServerLib.Interfaces.Client;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Client;
using MeridianServerLib.Models.Operations;
using MeridianTestContracts;
using MeridianTestContracts.Requests;
using MeridianTestContracts.Responses;

namespace MeridianLoadTestClient;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var options = LoadOptions.Parse(args);

        Console.WriteLine("Meridian load test");
        Console.WriteLine($"Target: {options.Host}:{options.Port}");
        Console.WriteLine($"Clients: {options.Clients}; duration: {options.Duration}; warmup: {options.Warmup}; operation: {options.Operation}; timeout: {options.Timeout}");

        var clients = new List<LoadClient>(options.Clients);
        for (var index = 0; index < options.Clients; index++)
        {
            var client = new LoadClient(options.Host, options.Port);
            clients.Add(client);
        }

        var connected = await ConnectAllAsync(clients, options.ConnectTimeout).ConfigureAwait(false);
        if (!connected)
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }

            Console.Error.WriteLine("Failed to connect every load client before timeout.");
            return 1;
        }

        if (options.Warmup > TimeSpan.Zero)
        {
            Console.WriteLine("Warmup...");
            await RunWorkersAsync(clients, options, options.Warmup, collectMetrics: false).ConfigureAwait(false);
        }

        Console.WriteLine("Running...");
        var stopwatch = Stopwatch.StartNew();
        var metrics = await RunWorkersAsync(clients, options, options.Duration, collectMetrics: true).ConfigureAwait(false);
        stopwatch.Stop();

        foreach (var client in clients)
        {
            client.Dispose();
        }

        PrintResult(metrics, stopwatch.Elapsed);
        return metrics.Success == 0 ? 2 : 0;
    }

    private static async Task<bool> ConnectAllAsync(IReadOnlyCollection<LoadClient> clients, TimeSpan timeout)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        var tasks = clients.Select(client => client.ConnectAsync(timeoutSource.Token)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.All(result => result);
    }

    private static async Task<LoadMetrics> RunWorkersAsync(
        IReadOnlyList<LoadClient> clients,
        LoadOptions options,
        TimeSpan duration,
        bool collectMetrics)
    {
        using var runTokenSource = new CancellationTokenSource(duration);
        var metrics = new LoadMetrics();
        var tasks = clients
            .Select((client, index) => RunWorkerAsync(client, index, options, metrics, runTokenSource.Token, collectMetrics))
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return metrics;
    }

    private static async Task RunWorkerAsync(
        LoadClient client,
        int clientIndex,
        LoadOptions options,
        LoadMetrics metrics,
        CancellationToken cancellationToken,
        bool collectMetrics)
    {
        var sequence = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var requestStopwatch = Stopwatch.StartNew();

            try
            {
                await SendOperationAsync(client, options, clientIndex, sequence, cancellationToken).ConfigureAwait(false);
                requestStopwatch.Stop();

                if (collectMetrics)
                {
                    metrics.AddSuccess(requestStopwatch.Elapsed.TotalMilliseconds);
                }
            }
            catch (TimeoutException)
            {
                if (collectMetrics)
                {
                    metrics.AddTimeout();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                if (collectMetrics)
                {
                    metrics.AddError();
                }
            }

            sequence++;
        }
    }

    private static Task SendOperationAsync(
        LoadClient client,
        LoadOptions options,
        int clientIndex,
        int sequence,
        CancellationToken cancellationToken)
    {
        return options.Operation switch
        {
            LoadOperation.Ping => client.SendAsync<PingResponse>(new PingRequest(), options.Timeout, cancellationToken),
            LoadOperation.Echo => client.SendAsync<EchoResponse>(
                new EchoRequest(CreatePayload(options.PayloadSize, clientIndex, sequence)),
                options.Timeout,
                cancellationToken),
            LoadOperation.Sum => client.SendAsync<SumResponse>(
                new SumRequest(clientIndex, sequence),
                options.Timeout,
                cancellationToken),
            LoadOperation.Mix => SendMixedOperationAsync(client, options, clientIndex, sequence, cancellationToken),
            _ => throw new InvalidOperationException("Unknown operation: " + options.Operation)
        };
    }

    private static Task SendMixedOperationAsync(
        LoadClient client,
        LoadOptions options,
        int clientIndex,
        int sequence,
        CancellationToken cancellationToken)
    {
        return (sequence % 3) switch
        {
            0 => client.SendAsync<PingResponse>(new PingRequest(), options.Timeout, cancellationToken),
            1 => client.SendAsync<EchoResponse>(
                new EchoRequest(CreatePayload(options.PayloadSize, clientIndex, sequence)),
                options.Timeout,
                cancellationToken),
            _ => client.SendAsync<SumResponse>(new SumRequest(clientIndex, sequence), options.Timeout, cancellationToken)
        };
    }

    private static string CreatePayload(int size, int clientIndex, int sequence)
    {
        if (size <= 0)
        {
            return string.Empty;
        }

        var prefix = $"{clientIndex}:{sequence}:";
        if (prefix.Length >= size)
        {
            return prefix.Substring(0, size);
        }

        return prefix + new string('x', size - prefix.Length);
    }

    private static void PrintResult(LoadMetrics metrics, TimeSpan elapsed)
    {
        var latencies = metrics.GetSortedLatencies();
        var total = metrics.Success + metrics.Errors + metrics.Timeouts;
        var rps = metrics.Success / Math.Max(elapsed.TotalSeconds, 0.001);

        Console.WriteLine();
        Console.WriteLine("Results");
        Console.WriteLine($"Elapsed: {elapsed.TotalSeconds:N1}s");
        Console.WriteLine($"Total attempts: {total}");
        Console.WriteLine($"Success: {metrics.Success}");
        Console.WriteLine($"Errors: {metrics.Errors}");
        Console.WriteLine($"Timeouts: {metrics.Timeouts}");
        Console.WriteLine($"RPS: {rps:N1}");

        if (latencies.Length == 0)
        {
            Console.WriteLine("Latency: no successful requests");
            return;
        }

        Console.WriteLine(
            "Latency ms: " +
            $"avg={latencies.Average():N2}; " +
            $"min={latencies[0]:N2}; " +
            $"p50={Percentile(latencies, 0.50):N2}; " +
            $"p95={Percentile(latencies, 0.95):N2}; " +
            $"p99={Percentile(latencies, 0.99):N2}; " +
            $"max={latencies[^1]:N2}");
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        index = Math.Clamp(index, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }
}

internal sealed class LoadClient :
    IOperationsReceiver,
    INetworkSender,
    ILogger,
    MeridianRequestSystem.RequestSystem.Utilities.ILogger,
    IDisposable
{
    private readonly ClientPeer _clientPeer;
    private readonly DataServerNetworkBus _networkBus;
    private readonly TaskCompletionSource<bool> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LoadClient(string host, int port)
    {
        _clientPeer = new ClientPeer(host, port, this);
        _clientPeer.SetOperationsReceiver(this);
        _clientPeer.ClientConnectionStatusChanged += OnConnectionStatusChanged;

        _networkBus = new DataServerNetworkBus();
        _networkBus.Init(this);
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        _clientPeer.Connect();

        var completedTask = await Task.WhenAny(_connected.Task, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
        return completedTask == _connected.Task && _connected.Task.Result;
    }

    public Task<TResponse> SendAsync<TResponse>(
        IDataNetworkRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        where TResponse : IDataNetworkResponse
    {
        return _networkBus.SendRequestAsync<TResponse>(request, timeout, cancellationToken);
    }

    public void Send(byte code, Dictionary<byte, object> package, bool isNecessarily)
    {
        _clientPeer.Send(new OperationData(code, package));
    }

    public void OnOperationReceived(OperationData operation)
    {
        if (operation.OperationCode == TestOperationCodes.RequestSystem)
        {
            _networkBus.IncomingResponse(operation.Parameters);
        }
    }

    public void Log(string message)
    {
    }

    public void LogError(string message, Exception exception)
    {
    }

    public void WriteLog(string msg)
    {
    }

    public void Dispose()
    {
        _clientPeer.ClientConnectionStatusChanged -= OnConnectionStatusChanged;
        _clientPeer.Discard();
    }

    private void OnConnectionStatusChanged(NetworkClientConnectionStatus status)
    {
        if (status == NetworkClientConnectionStatus.Connected)
        {
            _connected.TrySetResult(true);
        }
    }
}

internal sealed class LoadMetrics
{
    private readonly ConcurrentBag<double> _latencies = new();
    private long _success;
    private long _errors;
    private long _timeouts;

    public long Success => Interlocked.Read(ref _success);
    public long Errors => Interlocked.Read(ref _errors);
    public long Timeouts => Interlocked.Read(ref _timeouts);

    public void AddSuccess(double latencyMs)
    {
        Interlocked.Increment(ref _success);
        _latencies.Add(latencyMs);
    }

    public void AddError()
    {
        Interlocked.Increment(ref _errors);
    }

    public void AddTimeout()
    {
        Interlocked.Increment(ref _timeouts);
    }

    public double[] GetSortedLatencies()
    {
        var values = _latencies.ToArray();
        Array.Sort(values);
        return values;
    }
}

internal enum LoadOperation
{
    Ping,
    Echo,
    Sum,
    Mix
}

internal sealed class LoadOptions
{
    public string Host { get; private set; } = "127.0.0.1";
    public int Port { get; private set; } = 4555;
    public int Clients { get; private set; } = 32;
    public TimeSpan Duration { get; private set; } = TimeSpan.FromSeconds(30);
    public TimeSpan Warmup { get; private set; } = TimeSpan.FromSeconds(3);
    public TimeSpan Timeout { get; private set; } = TimeSpan.FromSeconds(10);
    public TimeSpan ConnectTimeout { get; private set; } = TimeSpan.FromSeconds(10);
    public LoadOperation Operation { get; private set; } = LoadOperation.Mix;
    public int PayloadSize { get; private set; } = 32;

    public static LoadOptions Parse(string[] args)
    {
        var options = new LoadOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            string NextValue()
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("Missing value for " + arg);
                }

                index++;
                return args[index];
            }

            switch (arg)
            {
                case "--host":
                    options.Host = NextValue();
                    break;
                case "--port":
                    options.Port = int.Parse(NextValue());
                    break;
                case "--clients":
                    options.Clients = int.Parse(NextValue());
                    break;
                case "--duration-sec":
                    options.Duration = TimeSpan.FromSeconds(double.Parse(NextValue()));
                    break;
                case "--warmup-sec":
                    options.Warmup = TimeSpan.FromSeconds(double.Parse(NextValue()));
                    break;
                case "--timeout-ms":
                    options.Timeout = TimeSpan.FromMilliseconds(double.Parse(NextValue()));
                    break;
                case "--connect-timeout-sec":
                    options.ConnectTimeout = TimeSpan.FromSeconds(double.Parse(NextValue()));
                    break;
                case "--operation":
                    options.Operation = Enum.Parse<LoadOperation>(NextValue(), ignoreCase: true);
                    break;
                case "--payload-size":
                    options.PayloadSize = int.Parse(NextValue());
                    break;
                case "--help":
                case "-h":
                    PrintUsageAndExit();
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arg);
            }
        }

        if (options.Clients <= 0)
        {
            throw new ArgumentException("--clients must be greater than zero.");
        }

        if (options.Port <= 0)
        {
            throw new ArgumentException("--port must be greater than zero.");
        }

        if (options.PayloadSize < 0)
        {
            throw new ArgumentException("--payload-size cannot be negative.");
        }

        return options;
    }

    private static void PrintUsageAndExit()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  MeridianLoadTestClient --host 192.168.0.20 --port 4555 --clients 64 --duration-sec 30 --operation mix");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --operation ping|echo|sum|mix");
        Console.WriteLine("  --payload-size <bytes>     Used by echo/mix.");
        Console.WriteLine("  --timeout-ms <ms>");
        Environment.Exit(0);
    }
}
