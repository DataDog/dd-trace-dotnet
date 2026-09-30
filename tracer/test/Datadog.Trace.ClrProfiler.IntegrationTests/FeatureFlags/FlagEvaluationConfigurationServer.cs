// <copyright file="FlagEvaluationConfigurationServer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.FeatureFlags;

// Run-owned configuration fixture; never modifies a shared staging configuration.
internal sealed class FlagEvaluationConfigurationServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Task _loop;
    private Tuple<byte[], string> _response;

    public FlagEvaluationConfigurationServer(string response)
    {
        _response = CreateResponse(response);
        Url = $"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}/";
        _listener.Prefixes.Add(Url);
        _listener.Start();
        _loop = Task.Run(ServeAsync);
    }

    public string Url { get; }

    public ConcurrentQueue<string> Requests { get; } = new();

    public Action AdvanceConfiguration { get; set; }

    public void SetResponse(string response) => Volatile.Write(ref _response, CreateResponse(response));

    public void Dispose()
    {
        _listener.Close();
        _loop.GetAwaiter().GetResult();
    }

    private static Tuple<byte[], string> CreateResponse(string response)
        => Tuple.Create(Encoding.UTF8.GetBytes(response), "\"" + Guid.NewGuid().ToString("N") + "\"");

    private async Task ServeAsync()
    {
        try
        {
            while (_listener.IsListening)
            {
                var context = await _listener.GetContextAsync();
                var path = context.Request.Url.AbsolutePath;
                Requests.Enqueue(path);
                if (path == "/advance")
                {
                    AdvanceConfiguration?.Invoke();
                }

                var response = Volatile.Read(ref _response);
                context.Response.Headers["ETag"] = response.Item2;
                if (path != "/advance" && context.Request.Headers["If-None-Match"] == response.Item2)
                {
                    context.Response.StatusCode = 304;
                    context.Response.Close();
                    continue;
                }

                var body = response.Item1;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body, 0, body.Length);
                context.Response.Close();
            }
        }
        catch (HttpListenerException) when (!_listener.IsListening)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
