using MeridianRequestSystem.RequestSystem.Bus;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianServerLib.Interfaces.Client;
using MeridianServerLib.LogsLayer.Interfaces;
using MeridianServerLib.Models.Client;
using MeridianServerLib.Models.Operations;
using MeridianTestContracts;
using MeridianTestContracts.Requests;
using MeridianTestContracts.Responses;

namespace MeridianTestClient;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var host = args.Length > 0 ? args[0] : "127.0.0.1";
        var port = args.Length > 1 && int.TryParse(args[1], out var parsedPort) ? parsedPort : 4555;

        using var client = new MeridianTestClient(host, port);

        if (!await client.ConnectAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false))
        {
            Console.Error.WriteLine("Connection timeout. Start MeridianServer with MeridianTestLogic first.");
            return 1;
        }

        Console.WriteLine("Connected to Meridian test layer.");
        Console.WriteLine("Commands: ping | echo <text> | sum <a> <b> | exit");

        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line == null) break;

            var command = line.Trim();
            if (command.Length == 0) continue;
            if (command.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            try
            {
                await RunCommandAsync(client, command).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Command failed: " + ex.Message);
            }
        }

        return 0;
    }

    private static async Task RunCommandAsync(MeridianTestClient client, string command)
    {
        if (command.Equals("ping", StringComparison.OrdinalIgnoreCase))
        {
            var response = await client.SendAsync<PingResponse>(new PingRequest()).ConfigureAwait(false);
            Console.WriteLine($"pong: {response.Message}; server utc: {response.ServerTimeUtc}");
            return;
        }

        if (command.StartsWith("echo ", StringComparison.OrdinalIgnoreCase))
        {
            var message = command.Substring("echo ".Length);
            var response = await client.SendAsync<EchoResponse>(new EchoRequest(message)).ConfigureAwait(false);
            Console.WriteLine($"echo: {response.Message}; length: {response.Length}");
            return;
        }

        if (command.StartsWith("sum ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || !int.TryParse(parts[1], out var a) || !int.TryParse(parts[2], out var b))
            {
                Console.WriteLine("Usage: sum <a> <b>");
                return;
            }

            var response = await client.SendAsync<SumResponse>(new SumRequest(a, b)).ConfigureAwait(false);
            Console.WriteLine($"sum: {response.Result}");
            return;
        }

        Console.WriteLine("Unknown command.");
    }
}

internal sealed class MeridianTestClient :
    IOperationsReceiver,
    INetworkSender,
    ILogger,
    MeridianRequestSystem.RequestSystem.Utilities.ILogger,
    IDisposable
{
    private readonly ClientPeer _clientPeer;
    private readonly DataServerNetworkBus _networkBus;
    private readonly TaskCompletionSource<bool> _connected = new();

    public MeridianTestClient(string host, int port)
    {
        _clientPeer = new ClientPeer(host, port, this);
        _clientPeer.SetOperationsReceiver(this);
        _clientPeer.ClientConnectionStatusChanged += OnConnectionStatusChanged;

        _networkBus = new DataServerNetworkBus();
        _networkBus.Init(this, this);
    }

    public async Task<bool> ConnectAsync(TimeSpan timeout)
    {
        _clientPeer.Connect();

        var completedTask = await Task.WhenAny(_connected.Task, Task.Delay(timeout)).ConfigureAwait(false);
        return completedTask == _connected.Task && _connected.Task.Result;
    }

    public Task<TResponse> SendAsync<TResponse>(
        MeridianRequestSystem.RequestSystem.Interfaces.IDataNetworkRequest request)
        where TResponse : MeridianRequestSystem.RequestSystem.Interfaces.IDataNetworkResponse
    {
        return _networkBus.SendRequestAsync<TResponse>(request, TimeSpan.FromSeconds(10));
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
        Console.WriteLine(message);
    }

    public void LogError(string message, Exception exception)
    {
        Console.Error.WriteLine(message + Environment.NewLine + exception);
    }

    public void WriteLog(string msg)
    {
        Console.WriteLine(msg);
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
