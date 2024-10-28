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
        var spanContext = SpanContextPropagator.Instance.Extract(headers);
        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.AwsLambda);

        var span = spanContext != null ? tracer.StartSpan(PlaceholderOperationName, tags: null, parent: spanContext, serviceName: PlaceholderServiceName, addToTraceContext: false) : tracer.StartSpan(PlaceholderOperationName, tags: null, serviceName: PlaceholderServiceName, addToTraceContext: false);

        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.AwsLambda);
        return tracer.TracerManager.ScopeManager.Activate(span, false);
    }

    internal static Scope SendStartInvocation(ILambdaExtensionRequest requestBuilder, string data, IDictionary<string, string> context)
    {
        var request = requestBuilder.GetStartInvocationRequest();
        WriteRequestPayload(request, data);
        WriteRequestHeaders(request, context);
        var response = (HttpWebResponse)request.GetResponse();

        var headers = response.Headers.Wrap();
        if (!ValidateOkStatus(response))
        {
            return null;
        }

        var tracer = Tracer.Instance;
        return CreatePlaceholderScope(tracer, headers);
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
