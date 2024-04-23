// <copyright file="MockLambdaExtension.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading;
using Datadog.Trace.Util;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

public class MockLambdaExtension : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Thread _listenerThread;

    public MockLambdaExtension(bool shouldSendContext, int port = 9004, ITestOutputHelper? output = null)
    {
        // try up to 5 times to acquire the port before giving up
        var retries = 5;
        ShouldSendContext = shouldSendContext;
        Output = output;
        while (true)
        {
            // seems like we can't reuse a listener if it fails to start,
            // so create a new listener each time we retry
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Prefixes.Add($"http://integrationtests:{port}/");

            try
            {
                listener.Start();

                // successfully listening
                Port = port;
                _listener = listener;

                _listenerThread = new Thread(HandleHttpRequests);
                _listenerThread.Start();

                return;
            }
            catch (HttpListenerException) when (retries > 0)
            {
                // only catch the exception if there are retries left
                retries--;
            }
            catch
            {
                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
                throw;
            }

            // always close listener if exception is thrown,
            // whether it was caught or not
            listener.Close();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the extension should return a TraceId and Sampling priority when
    /// it receives a StartInvocation
    /// </summary>
    public bool ShouldSendContext { get; }

    /// <summary>
    /// Gets the TCP port that this Agent is listening on.
    /// </summary>
    public int Port { get; }

    public ITestOutputHelper? Output { get; set; }

    public ConcurrentStack<StartExtensionRequest> StartInvocations { get; } = new();

    public ConcurrentStack<EndExtensionRequest> EndInvocations { get; } = new();

    public void Dispose()
    {
        _listener?.Close();
    }

    protected virtual void HandleHttpRequest(HttpListenerContext ctx)
    {
        if (ctx.Request.Url?.PathAndQuery.StartsWith("/lambda/start-invocation") ?? false)
        {
            var headers = new NameValueCollection(ctx.Request.Headers);
            string body;
            using (var sr = new StreamReader(ctx.Request.InputStream))
            {
                body = sr.ReadToEnd();
            }

            if (ShouldSendContext)
            {
                var traceId = RandomIdGenerator.Shared.NextSpanId();
                var samplingPriority = SamplingPriorityValues.AutoKeep;
                StartInvocations.Push(new StartExtensionRequest(headers, body, traceId, samplingPriority));
                Output?.WriteLine($"[LambdaExtension]Received start-invocation. Sending context trace ID {traceId}");

                ctx.Response.Headers.Set("x-datadog-trace-id", traceId.ToString());
                ctx.Response.Headers.Set("x-datadog-sampling-priority", samplingPriority.ToString());
            }
            else
            {
                StartInvocations.Push(new StartExtensionRequest(headers, body));
                Output?.WriteLine($"[LambdaExtension]Received start-invocation. No context");
            }
        }
        else if (ctx.Request.Url?.PathAndQuery.StartsWith("/lambda/end-invocation") ?? false)
        {
            var headers = new NameValueCollection(ctx.Request.Headers);
            string body;
            using (var sr = new StreamReader(ctx.Request.InputStream))
            {
                body = sr.ReadToEnd();
            }

            ulong? traceId = ulong.TryParse(headers.Get("x-datadog-trace-id"), out var t) ? t : null;
            ulong? spanId = ulong.TryParse(headers.Get("x-datadog-span-id"), out var s) ? s : null;
            int? samplingPriority = int.TryParse(headers.Get("x-datadog-sampling-priority"), out var p) ? p : null;
            bool isError = headers.Get("x-datadog-invocation-error") == "true";
            string? errorMsg = headers.Get("x-datadog-invocation-error-msg") ?? null;
            string? errorType = headers.Get("x-datadog-invocation-error-type") ?? null;
            string? errorStack = headers.Get("x-datadog-invocation-error-stack") ?? null;
            var invocation = new EndExtensionRequest(headers, body,  traceId, spanId, samplingPriority, isError, errorMsg, errorType, errorStack);

            EndInvocations.Push(invocation);
            Output?.WriteLine($"[LambdaExtension]Received end-invocation. traceId:{traceId}, spanId:{spanId}");
        }
        else
        {
            throw new Exception("Unexpected request to " + ctx.Request.Url);
        }

        ctx.Response.StatusCode = 200;
        ctx.Response.Close();
    }

    private void HandleHttpRequests()
    {
        while (_listener.IsListening)
        {
            try
            {
                var ctx = _listener.GetContext();
                try
                {
                    HandleHttpRequest(ctx);
                }
                catch (Exception ex)
                {
                    Output?.WriteLine("[LambdaExtension]Error processing web request" + ex);
                    ctx.Response.Close();
                }
            }
            catch (HttpListenerException)
            {
                // listener was stopped,
                // ignore to let the loop end and the method return
            }
            catch (ObjectDisposedException)
            {
                // the response has been already disposed.
            }
            catch (InvalidOperationException)
            {
                // this can occur when setting Response.ContentLength64, with the framework claiming that the response has already been submitted
                // for now ignore, and we'll see if this introduces downstream issues
            }
            catch (Exception) when (!_listener.IsListening)
            {
                // we don't care about any exception when listener is stopped
            }
        }
    }

    public class StartExtensionRequest
    {
        public StartExtensionRequest(NameValueCollection headers, string body, ulong traceId, int samplingPriority)
        {
            Headers = headers;
            Body = body;
            TraceId = traceId;
            SamplingPriority = samplingPriority;
        }

        public StartExtensionRequest(NameValueCollection headers, string body)
        {
            Headers = headers;
            Body = body;
        }

        public NameValueCollection Headers { get; }

        public string Body { get; }

        public ulong? TraceId { get; }

        public int? SamplingPriority { get; }

        public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;
    }

    public class EndExtensionRequest
    {
        public EndExtensionRequest(
            NameValueCollection headers,
            string body,
            ulong? traceId,
            ulong? spanId,
            int? samplingPriority,
            bool isError,
            string? errorMsg,
            string? errorType,
            string? errorStack)
        {
            Headers = headers;
            Body = body;
            TraceId = traceId;
            SpanId = spanId;
            SamplingPriority = samplingPriority;
            IsError = isError;
            ErrorMsg = errorMsg;
            ErrorType = errorType;
            ErrorStack = errorStack;
        }

        public NameValueCollection Headers { get; }

        public string Body { get; }

        public ulong? TraceId { get; }

        public ulong? SpanId { get; }

        public int? SamplingPriority { get; }

        public bool IsError { get; }

        public string? ErrorMsg { get; }

        public string? ErrorType { get; }

        public string? ErrorStack { get; }

        public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;
    }
}
