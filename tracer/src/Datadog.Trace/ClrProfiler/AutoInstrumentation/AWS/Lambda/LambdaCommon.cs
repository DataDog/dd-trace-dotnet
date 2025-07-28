// <copyright file="LambdaCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

internal abstract class LambdaCommon
{
    private const string PlaceholderServiceName = "placeholder-service";
    private const string PlaceholderOperationName = "placeholder-operation";
    private const double ServerlessMaxWaitingFlushTime = 3;
    private const string LogLevelEnvName = "DD_LOG_LEVEL";

    internal static Scope CreatePlaceholderScope(Tracer tracer, NameValueHeadersCollection headers)
    {
        Log("Creating placeholder scope for Lambda invocation", debug: true);
        var context = tracer.TracerManager.SpanContextPropagator.Extract(headers).MergeBaggageInto(Baggage.Current);

        var span = tracer.StartSpan(
            PlaceholderOperationName,
            tags: null,
            parent: context.SpanContext,
            serviceName: PlaceholderServiceName,
            addToTraceContext: false);

        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.AwsLambda);
        return tracer.TracerManager.ScopeManager.Activate(span, false);
    }

    internal static Scope SendStartInvocation(ILambdaExtensionRequest requestBuilder, string data, IDictionary<string, string> context)
    {
        Log("Starting Lambda invocation", debug: false);
        var request = requestBuilder.GetStartInvocationRequest();
        WriteRequestPayload(request, data);
        WriteRequestHeaders(request, context);
        using var response = (HttpWebResponse)request.GetResponse();

        var headers = response.Headers.Wrap();
        if (!ValidateOkStatus(response))
        {
            Log("Failed to validate OK status for start invocation", debug: false);
            return null;
        }

        var tracer = Tracer.Instance;
        return CreatePlaceholderScope(tracer, headers);
    }

    internal static void SendEndInvocation(ILambdaExtensionRequest requestBuilder, Scope scope, bool isError, string data)
    {
        Log($"Ending Lambda invocation (isError: {isError})", debug: false);
        var request = requestBuilder.GetEndInvocationRequest(scope, isError);
        WriteRequestPayload(request, data);
        using var response = (HttpWebResponse)request.GetResponse();

        if (!ValidateOkStatus(response))
        {
            Log("Extension does not send a status 200 OK", debug: false);
        }
    }

    internal static async Task EndInvocationAsync(string returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
    {
        Log("Starting end invocation async process", debug: false);
        try
        {
            Log("Attempting to flush for current invocation", debug: false);

            var useDsmFlushLogic = Environment.GetEnvironmentVariable("USE_DSM_FLUSH_LOGIC");
            if (string.Equals(useDsmFlushLogic, "true", StringComparison.OrdinalIgnoreCase))
            {
                Log("Flushing with DSM", debug: false);
                await Task.WhenAll(
                    Tracer.Instance.TracerManager.AgentWriter.FlushTracesAsync()
                        .WaitAsync(TimeSpan.FromSeconds(ServerlessMaxWaitingFlushTime)),
                    Tracer.Instance.TracerManager.DataStreamsManager.FlushAsync()
                        .WaitAsync(TimeSpan.FromSeconds(ServerlessMaxWaitingFlushTime)))
                    .ConfigureAwait(false);
            }
            else
            {
                Log("Flushing without DSM", debug: false);
                await Tracer.Instance.TracerManager.AgentWriter.FlushTracesAsync()
                    .WaitAsync(TimeSpan.FromSeconds(ServerlessMaxWaitingFlushTime))
                    .ConfigureAwait(false);
            }

            Log("Successfully flushed for current invocation", debug: false);
        }
        catch (Exception ex)
        {
            Log("Could not flush to the extension", ex, false);
        }

        try
        {
            if (exception != null && scope is { Span: var span })
            {
                Log($"Setting exception on span: {exception.Message}", debug: false);
                span.SetException(exception);
            }

            SendEndInvocation(requestBuilder, scope, exception != null, returnValue);
        }
        catch (Exception ex)
        {
            Log("Could not send payload to the extension", ex, false);
        }

        scope?.Dispose();
        Log("Completed end invocation async process", debug: false);
    }

    private static bool ValidateOkStatus(HttpWebResponse response)
    {
        var statusCode = response.StatusCode;
        Log($"The extension responds with statusCode = {statusCode}", debug: true);
        return statusCode == HttpStatusCode.OK;
    }

    private static void WriteRequestPayload(WebRequest request, string data)
    {
        Log("Writing request payload", debug: true);
        var byteArray = Encoding.UTF8.GetBytes(data);
        request.ContentLength = byteArray.Length;
        var dataStream = request.GetRequestStream();
        dataStream.Write(byteArray, 0, byteArray.Length);
        dataStream.Close();
    }

    private static void WriteRequestHeaders(WebRequest request, IDictionary<string, string> context)
    {
        Log("Writing request headers", debug: true);
        if (context != null)
        {
            foreach (var kv in context)
            {
                request.Headers.Add(kv.Key, kv.Value);
            }
        }
    }

    internal static void Log(string message, Exception ex = null, bool debug = true)
    {
        Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss:fff} [DD_TRACE_DOTNET] {message} {ex?.ToString().Replace("\n", "\\n")}");
    }
}
#endif
