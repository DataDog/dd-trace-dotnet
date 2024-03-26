using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Samples.Grpc.Services;
#nullable enable

namespace Samples.Grpc;

public class ClientWorker
{
    private readonly Logger<ClientWorker> _logger;
    private static readonly ErrorType[] ErrorTypes = (ErrorType[])Enum.GetValues(typeof(ErrorType));
    private static readonly ActivitySource _source = new("Samples.Grpc");

    public ClientWorker(Logger<ClientWorker> logger)
    {
        _logger = logger;
        var activityListener = new ActivityListener
        {
            ActivityStarted = activity => Console.WriteLine($"{activity.DisplayName}:{activity.Id} - Started"),
            ActivityStopped = activity => Console.WriteLine($"{activity.DisplayName}:{activity.Id} - Stopped"),
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };

        ActivitySource.AddActivityListener(activityListener);
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!Program.AppListening && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Waiting for app started handling requests");
            await Task.Delay(100, stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Cancellation requested.");
            return;
        }

        var serverAddress = $"127.0.0.1:{Program.ServerPort}";
        _logger.LogInformation("App started. Sending requests to " + serverAddress);

        Task delay = Task.CompletedTask;
        try
        {
            var channel = new Channel(serverAddress, ChannelCredentials.Insecure);
            var callInvoker = channel.Intercept(
                meta =>
                {
                    meta.Add("client-value1", "some-client-value");
                    meta.Add("client-value2", "other-client-value");
                    return meta;
                });

            _logger.LogInformation("Creating GRPC client");
            var client = new Greeter.GreeterClient(callInvoker);

            await SendVerySlowRequestAsync(client);
            delay = Task.Delay(6_000); // longer than the slow request duration

            await SendUnaryRequestAsync(client);
            await SendServerStreamingRequest(client, stoppingToken);
            await SendClientStreamingRequest(client, stoppingToken);
            await SendBothStreamingRequest(client, stoppingToken);
            await SendErrorsAsync(client);

            SendUnaryRequest(client);
            SendErrors(client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending request");
            Program.ExitCode = 1;
        }

        // This is kinda horrible, but the "slow requests" don't close until the
        // service has completed, so won't close the span until then
        await delay;

        _logger.LogInformation("Stopping application");
    }

    private async Task SendUnaryRequestAsync(Greeter.GreeterClient client)
    {
        using var activity = StartActivity();
        _logger.LogInformation("Sending unary async request to self");
        var reply = await client.UnaryAsync(
                        new HelloRequest { Name = "GreeterClient" });

        _logger.LogInformation("Received async reply message: "+ reply.Message);
    }

    private void SendUnaryRequest(Greeter.GreeterClient client)
    {
        using var activity = StartActivity();
        _logger.LogInformation("Sending unary request to self");
        var reply = client.Unary(new HelloRequest { Name = "GreeterClient" });

        _logger.LogInformation("Received reply message: " + reply.Message);
    }

    private async Task SendServerStreamingRequest(Greeter.GreeterClient client, CancellationToken ct)
    {
        using var activity = StartActivity();
        _logger.LogInformation("Sending streaming server request to self");

        using var messages = client.StreamingFromServer(
            new HelloRequest { Name = "GreeterClient" });

        while (await messages.ResponseStream.MoveNext(ct))
        {
            _logger.LogInformation("Received streaming server message: " + messages.ResponseStream.Current.Message);
        }

        _logger.LogInformation("Received all streaming server responses");
    }

    private async Task SendClientStreamingRequest(Greeter.GreeterClient client, CancellationToken ct)
    {
        using var activity = StartActivity();
        _logger.LogInformation("Sending streaming client requests to self");

        using var call = client.StreamingFromClient();

        for (int i = 0; i < 5; i++)
        {
            var helloRequest = new HelloRequest { Name = "GreeterClient" + i };
            _logger.LogInformation("Sending streaming client message: " + helloRequest.Name);
            await call.RequestStream.WriteAsync(helloRequest);
        }

        await call.RequestStream.CompleteAsync();
        var response = await call;

        _logger.LogInformation("Received final streaming client response " + response.Message);
    }

    private async Task SendBothStreamingRequest(Greeter.GreeterClient client, CancellationToken ct)
    {
        using var activity = StartActivity();
        _logger.LogInformation("Sending streaming server request to self");

        using var call = client.StreamingBothWays();

        var readTask = Task.Run(async () =>
        {
            while (await call.ResponseStream.MoveNext())
            {
                var response = call.ResponseStream.Current;
                _logger.LogInformation("Received both streaming message: " + response.Message);
            }
        });

        for (int i = 0; i < 5; i++)
        {
            var helloRequest = new HelloRequest { Name = "GreeterClient" + i };
            _logger.LogInformation("Sending both streaming message: " + helloRequest.Name);
            await call.RequestStream.WriteAsync(helloRequest);
        }

        await call.RequestStream.CompleteAsync();
        await readTask;
        _logger.LogInformation("Both streaming server responses done");
    }

    private void SendErrors(Greeter.GreeterClient client)
    {
        foreach (var errorType in ErrorTypes)
        {
            using var activity = _source.StartActivity($"SendErrors_{errorType}");
            try
            {
                _logger.LogInformation("Sending err request to self with " + errorType);
                var reply = client.ErroringMethod(new CreateErrorRequest { ErrorType = (int)errorType });

                _logger.LogError("Received reply message: " +  reply.Message + "but expected exception");
                throw new InvalidOperationException("Expected an exception");
            }
            catch (RpcException ex)
            {
                _logger.LogInformation("Received RPC exception with StatusCode " + ex.Status);
                // expected
            }
        }
    }

    private async Task SendErrorsAsync(Greeter.GreeterClient client)
    {
        foreach (var errorType in ErrorTypes)
        {
            using var activity = _source.StartActivity($"SendErrorsAsync_{errorType}");
            try
            {
                _logger.LogInformation("Sending err request to self with " + errorType);
                var reply = await client.ErroringMethodAsync(new CreateErrorRequest { ErrorType = (int)errorType });

                _logger.LogError("Received reply message: " +  reply.Message + "but expected exception");
                throw new InvalidOperationException("Expected an exception");
            }
            catch (RpcException ex)
            {
                _logger.LogInformation("Received RPC exception with StatusCode " + ex.Status);
                // expected
            }
        }
    }

    private async Task SendVerySlowRequestAsync(Greeter.GreeterClient client)
    {
        using var activity = StartActivity();
        try
        {
            _logger.LogInformation("Sending very slow request to self");
            await client.VerySlowAsync(new HelloRequest { Name = "GreeterClient" }, deadline: DateTime.UtcNow.AddSeconds(2));

            throw new Exception("Received reply, when should have exceeded deadline");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogInformation("Received deadline exceeded response " + ex.Message);
        }
    }

    private void SendVerySlowRequest(Greeter.GreeterClient client)
    {
        using var activity = StartActivity();
        try
        {
            _logger.LogInformation("Sending very slow request to self");
            client.VerySlow(new HelloRequest { Name = "GreeterClient" }, deadline: DateTime.UtcNow.AddSeconds(2));

            throw new Exception("Received reply, when should have exceeded deadline");
        }
        catch(RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogInformation("Received deadline exceeded response " + ex.Message);
        }
    }

    private IDisposable StartActivity([CallerMemberName] string name = "")
    {
        var activity = _source.StartActivity(name);

        return activity is null
            ? throw new Exception($"Attempted to start a new activity for {name} method, but activity returned was null.")
            : activity;
    }

}
