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
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

internal abstract class LambdaCommon
{
    // Name of the placeholder span to be filtered out by the Lambda Extension
    private const string InvocationSpanName = "dd-tracer-serverless-span";
    private const double ServerlessMaxWaitingFlushTime = 3;
    private const string LogLevelEnvName = "DD_LOG_LEVEL";
    private const string LambdaRuntimeAwsRequestIdHeader = "lambda-runtime-aws-request-id";

    internal static Scope CreatePlaceholderScope(Tracer tracer, NameValueHeadersCollection headers, string awsRequestId = null)
    {
        var context = tracer.TracerManager.SpanContextPropagator.Extract(headers).MergeBaggageInto(Baggage.Current);

        var span = tracer.StartSpan(
            operationName: InvocationSpanName,
            tags: null,
            parent: context.SpanContext,
            serviceName: InvocationSpanName,
            addToTraceContext: true);

        // The Lambda extension uses the resource name to identify placeholder span
        span.ResourceName = InvocationSpanName;

        // Need to set request_id to copy tracer tags to the aws.lambda span
        if (awsRequestId != null)
        {
            span.SetTag("request_id", awsRequestId);
        }

        TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.AwsLambda);
        return tracer.TracerManager.ScopeManager.Activate(span, finishOnClose: true);
    }

    internal static Scope SendStartInvocation(ILambdaExtensionRequest requestBuilder, string data, ILambdaContext context)
    {
        var request = requestBuilder.GetStartInvocationRequest();
        WriteRequestPayload(request, data);
        WriteRequestHeaders(request, context?.ClientContext?.Custom);
        var awsRequestId = context?.AwsRequestId;
        if (awsRequestId != null)
        {
            request.Headers.Add(LambdaRuntimeAwsRequestIdHeader, awsRequestId);
        }

        using var response = (HttpWebResponse)request.GetResponse();

        var headers = response.Headers.Wrap();
        if (!ValidateOkStatus(response))
        {
            return null;
        }

        var tracer = Tracer.Instance;
        return CreatePlaceholderScope(tracer, headers, awsRequestId);
    }

    internal static void SendEndInvocation(ILambdaExtensionRequest requestBuilder, CallTargetState stateObject, bool isError, string data)
    {
        var request = requestBuilder.GetEndInvocationRequest(stateObject, isError);
        WriteRequestPayload(request, data);
        using var response = (HttpWebResponse)request.GetResponse();

        if (!ValidateOkStatus(response))
        {
            Log("Extension does not send a status 200 OK", debug: false);
        }
    }

    internal static async Task EndInvocationAsync(string returnValue, Exception exception, CallTargetState stateObject, ILambdaExtensionRequest requestBuilder)
    {
        var scope = stateObject.Scope;

        if (exception != null && scope is { Span: var span })
        {
            span.SetException(exception);
        }

        scope?.Dispose();

        try
        {
            await Task.WhenAll(
                Tracer.Instance.TracerManager.AgentWriter.FlushTracesAsync()
                    .WaitAsync(TimeSpan.FromSeconds(ServerlessMaxWaitingFlushTime)),
                Tracer.Instance.TracerManager.DataStreamsManager.FlushAsync()
                    .WaitAsync(TimeSpan.FromSeconds(ServerlessMaxWaitingFlushTime)))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log("Could not flush to the extension", ex, false);
        }

        try
        {
            SendEndInvocation(requestBuilder, stateObject, exception != null, returnValue);
        }
        catch (Exception ex)
        {
            Log("Could not send payload to the extension", ex, false);
        }
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
