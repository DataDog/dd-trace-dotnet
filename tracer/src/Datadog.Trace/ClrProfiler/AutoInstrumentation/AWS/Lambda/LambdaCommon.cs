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

    internal static Scope CreatePlaceholderScope(Tracer tracer, string traceId, ulong traceIdUpper64, string samplingPriority)
    {
        Span span;

        if (traceId == null)
        {
            Log("traceId not found");
            span = tracer.StartSpan(PlaceholderOperationName, tags: null, serviceName: PlaceholderServiceName, addToTraceContext: false);
        }
        else
        {
            if (traceIdUpper64 == 0)
            {
                Log($"creating the placeholder traceId = {traceId}");
                span = tracer.StartSpan(PlaceholderOperationName, tags: null, serviceName: PlaceholderServiceName, traceId: (TraceId)Convert.ToUInt64(traceId), addToTraceContext: false);
            }
            else
            {
                var traceIdLower64 = Convert.ToUInt64(traceId);
                Log($"creating the placeholder traceId = {traceIdUpper64}{traceIdLower64}");
                span = tracer.StartSpan(PlaceholderOperationName, tags: null, serviceName: PlaceholderServiceName, traceId: new TraceId(traceIdUpper64, traceIdLower64), addToTraceContext: false);
            }
        }

        if (samplingPriority == null)
        {
            Log("samplingPriority not found");
            _ = span.Context.TraceContext?.GetOrMakeSamplingDecision();
        }
        else
        {
            Log($"setting the placeholder sampling priority to = {samplingPriority}");
            span.Context.TraceContext?.SetSamplingPriority(Convert.ToInt32(samplingPriority), notifyDistributedTracer: false);
        }

        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.AwsLambda);
        return tracer.TracerManager.ScopeManager.Activate(span, false);
    }

    internal static Scope SendStartInvocation(ILambdaExtensionRequest requestBuilder, string data, IDictionary<string, string> context)
    {
        var request = requestBuilder.GetStartInvocationRequest();
        WriteRequestPayload(request, data);
        WriteRequestHeaders(request, context);
        var response = (HttpWebResponse)request.GetResponse();
        var traceId = response.Headers.Get(HttpHeaderNames.TraceId);
        var traceIdUpper64 = GetTraceIdUpper64(response.Headers.Get(HttpHeaderNames.PropagatedTags));
        var samplingPriority = response.Headers.Get(HttpHeaderNames.SamplingPriority);
        if (ValidateOkStatus(response))
        {
            return CreatePlaceholderScope(Tracer.Instance, traceId, traceIdUpper64, samplingPriority);
        }

        return null;
    }

    /// <summary>
    /// GetTraceIdUpper64 searches the x-datadog-tags tags for the upper 64 bits of the trace id.
    /// Per the 128 bit tracing RFC, the upper 64 bits are stored in the _dd.p.tid tag as a hex string.
    /// </summary>
    /// <param name="ddTags">The propagated tags from the x-datadog-tags header</param>
    /// <returns>the upper 64 bits of the traceId as a ulong</returns>
    internal static ulong GetTraceIdUpper64(string ddTags)
    {
        if (ddTags == null)
        {
            return 0;
        }

        var tags = ddTags.Split(',');
        foreach (var tag in tags)
        {
            var keyValue = tag.Trim().Split('=');
            if (keyValue.Length == 2 && keyValue[0] == "_dd.p.tid")
            {
                return Convert.ToUInt64(keyValue[1], 16);
            }
        }

        return 0;
    }

    internal static void SendEndInvocation(ILambdaExtensionRequest requestBuilder, Scope scope, bool isError, string data)
    {
        var request = requestBuilder.GetEndInvocationRequest(scope, isError);
        WriteRequestPayload(request, data);
        if (!ValidateOkStatus((HttpWebResponse)request.GetResponse()))
        {
            Log("Extension does not send a status 200 OK", debug: false);
        }
    }

    internal static async Task EndInvocationAsync(string returnValue, Exception exception, Scope scope, ILambdaExtensionRequest requestBuilder)
    {
        try
        {
            await Tracer.Instance.TracerManager.AgentWriter.FlushTracesAsync()
                        .WaitAsync(TimeSpan.FromSeconds(ServerlessMaxWaitingFlushTime))
                        .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log("Could not flush to the extension", ex, false);
        }

        try
        {
            if (exception != null && scope is { Span: var span })
            {
                span.SetException(exception);
            }

            SendEndInvocation(requestBuilder, scope, exception != null, returnValue);
        }
        catch (Exception ex)
        {
            Log("Could not send payload to the extension", ex, false);
        }

        scope?.Dispose();
    }

    private static bool ValidateOkStatus(HttpWebResponse response)
    {
        var statusCode = response.StatusCode;
        Log("The extension responds with statusCode = " + statusCode);
        return statusCode == HttpStatusCode.OK;
    }

    private static void WriteRequestPayload(WebRequest request, string data)
    {
        var byteArray = Encoding.UTF8.GetBytes(data);
        request.ContentLength = byteArray.Length;
        var dataStream = request.GetRequestStream();
        dataStream.Write(byteArray, 0, byteArray.Length);
        dataStream.Close();
    }

    private static void WriteRequestHeaders(WebRequest request, IDictionary<string, string> context)
    {
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
        if (!debug || EnvironmentHelpers.GetEnvironmentVariable(LogLevelEnvName)?.ToLower() == "debug")
        {
            Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss:fff} [DD_TRACE_DOTNET] {message} {ex?.ToString().Replace("\n", "\\n")}");
        }
    }
}
#endif
