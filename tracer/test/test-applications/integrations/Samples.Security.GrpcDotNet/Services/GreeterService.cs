using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Samples.Security.GrpcDotNet;

#nullable enable

namespace Samples.Security.GrpcDotNet.Services;

public class GreeterService : Greeter.GreeterBase
{
    private readonly ILogger<GreeterService> _logger;

    public GreeterService(ILogger<GreeterService> logger)
    {
        _logger = logger;
    }

    public override Task<HelloReply> Unary(HelloRequest request, ServerCallContext context)
    {
        LogMethod();
        TriggerVulnerability("Unary: " + request.Name);
        return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
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
            
            TriggerVulnerability("StreamingFromServer: " + request.Name);
            await responseStream.WriteAsync(new HelloReply { Message = $"Hello {i}" });
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    public override async Task<HelloReply> StreamingFromClient(IAsyncStreamReader<HelloRequest> requestStream, ServerCallContext context)
    {
        LogMethod();
        var names = new List<string>();
        await foreach (var message in requestStream.ReadAllAsync())
        {
            names.Add(message.Name);
        }

        return new HelloReply { Message = $"Hello {string.Join(", and ", names)}" };
    }

    public override async Task StreamingBothWays(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        LogMethod();
        await foreach (var message in requestStream.ReadAllAsync())
        {
            await responseStream.WriteAsync(new HelloReply { Message = $"Hello {message.Name}" });
        }
    }

    private void LogMethod([CallerMemberName] string? member = null)
    {
        _logger.LogInformation("Server invocation of {MemberName}", member);
    }

    // Trigger a command injection vulnerability to test the taint of the request
    private void TriggerVulnerability(string input)
    {
        try
        {
            Process.Start(input, "");
        }
        catch
        {
            // Will throw an exception, but ignore it
        }
    }
}
