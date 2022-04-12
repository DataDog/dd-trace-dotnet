using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Grpc.Core;

#nullable enable

namespace Samples.Grpc.Services;

public class GreeterService : Greeter.GreeterBase
{
    private readonly Logger<GreeterService> _logger;

    public GreeterService(Logger<GreeterService> logger)
    {
        _logger = logger;
    }

    public override async Task<HelloReply> ErroringMethod(CreateErrorRequest request, ServerCallContext context)
    {
        LogMethod();

        await context.WriteResponseHeadersAsync(new Metadata { { "x-my-test-meta", "FromTheServer" } });
        return (ErrorType)request.ErrorType switch
        {
            ErrorType.DataLoss => throw new RpcException(new Status(StatusCode.DataLoss, "My spices!")),
            ErrorType.NotFound => throw new RpcException(new Status(StatusCode.NotFound, "Where did it go?")),
            ErrorType.Cancelled => throw new RpcException(Status.DefaultCancelled),
            ErrorType.Throw or _ => throw new Exception("Oh noes, my grpc!"),
        };
    }

    public override Task<HelloReply> Unary(HelloRequest request, ServerCallContext context)
    {
        LogMethod();
        return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
    }

    public override async Task<HelloReply> VerySlow(HelloRequest request, ServerCallContext context)
    {
        LogMethod();
        await Task.Delay(5_000);
        return new HelloReply { Message = "Hello " + request.Name };
    }

    public override async Task StreamingFromServer(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        LogMethod();
        for (var i = 0; i < 5; i++)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            await responseStream.WriteAsync(new HelloReply { Message = $"Hello {i}" });
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    public override async Task<HelloReply> StreamingFromClient(IAsyncStreamReader<HelloRequest> requestStream, ServerCallContext context)
    {
        LogMethod();
        var names = new List<string>();
        while (await requestStream.MoveNext())
        {
            names.Add(requestStream.Current.Name);
        }

        return new HelloReply { Message = $"Hello {string.Join(", and ", names)}" };
    }

    public override async Task StreamingBothWays(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        LogMethod();
        while (await requestStream.MoveNext())
        {
            await responseStream.WriteAsync(new HelloReply { Message = $"Hello {requestStream.Current.Name}" });
        }
    }

    private void LogMethod([CallerMemberName] string? member = null)
    {
        _logger.LogInformation("Server invocation of " + member);
    }
}
