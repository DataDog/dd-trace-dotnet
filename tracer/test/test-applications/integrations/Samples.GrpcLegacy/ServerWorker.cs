using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Samples.Grpc.Services;

namespace Samples.Grpc;

public class ServerWorker
{
    private readonly Logger<ServerWorker> _logger;
    private readonly GreeterService _greeter;

    public ServerWorker(Logger<ServerWorker> logger, GreeterService greeter)
    {
        _logger = logger;
        _greeter = greeter;
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = WebServer.GetOpenPort();

        _logger.LogInformation("Starting GRPC server");
        var server = new Server
        {
            Services = { Greeter.BindService(_greeter).Intercept(new ServerInterceptor()) },
            Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
        };
        server.Start();

        _logger.LogInformation("Listening on port" + port);

        Program.ServerPort = port;
        Program.AppListening = true;

        try
        {
            await Task.Delay(-1, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // swallow
        }

        _logger.LogInformation("Cancellation requested, closing server");
        await server.ShutdownAsync();
        _logger.LogInformation("Server shutdown complete");
    }

    public class ServerInterceptor: Interceptor
    {
        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            AddMeta(context);
            return base.UnaryServerHandler(request, context, continuation);
        }

        public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            AddMeta(context);
            return base.ClientStreamingServerHandler(requestStream, context, continuation);
        }

        public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            AddMeta(context);
            return base.ServerStreamingServerHandler(request, responseStream, context, continuation);
        }

        public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            AddMeta(context);
            return base.DuplexStreamingServerHandler(requestStream, responseStream, context, continuation);
        }

        private static void AddMeta(ServerCallContext context)
        {
            context.ResponseTrailers.Add("server-value1", "some-server-value");
            context.ResponseTrailers.Add("server-value2", "other-server-value");

            // Check if we received the traceparent header that dd-trace should have injected
            // If the bug triggers, dd-trace fails to inject headers, so we'll receive the pre-populated one
            var traceparent = context.RequestHeaders.Get("traceparent");
            if (traceparent != null)
            {
                var receivedValue = traceparent.Value;
                // The client pre-populates with trace ID ending in "c1438cefe41d4293"
                // If we receive this, it means dd-trace failed to inject its own header
                if (receivedValue != null && receivedValue.Contains("0af7651916cd43dd8448eb211c80319c"))
                {
                    // Propagation failed! Add a response header so this shows up in snapshots
                    context.ResponseTrailers.Add("x-dd-propagation-failed", "true");
                    Console.WriteLine("ERROR: Received pre-populated traceparent header - dd-trace failed to inject!");
                }
            }
            else
            {
                // No traceparent header at all - propagation completely failed
                context.ResponseTrailers.Add("x-dd-propagation-failed", "no-header");
                Console.WriteLine("ERROR: No traceparent header received - propagation completely failed!");
            }
        }
    }
}
