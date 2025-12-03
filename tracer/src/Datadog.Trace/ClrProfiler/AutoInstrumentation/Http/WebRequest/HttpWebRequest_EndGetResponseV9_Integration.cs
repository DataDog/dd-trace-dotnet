// <copyright file="HttpWebRequest_EndGetResponseV9_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest;

/// <summary>
/// System.Net.WebResponse System.Net.HttpWebRequest::EndGetResponse(System.IAsyncResult) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = WebRequestCommon.NetCoreAssembly,
    TypeName = WebRequestCommon.HttpWebRequestTypeName,
    MethodName = "EndGetResponse",
    ReturnTypeName = WebRequestCommon.WebResponseTypeName,
    ParameterTypeNames = [ClrNames.IAsyncResult],
    MinimumVersion = "9",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = WebRequestCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class HttpWebRequest_EndGetResponseV9_Integration
{
    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        // Assumes that AllowWriteStreamBuffering doesn't change _between_ calling BeginGetRequest and EndGetResponse
        // That _might_ not be a valid assumption - a better option woudl be to
        if (instance is HttpWebRequest { AllowWriteStreamBuffering: false } request
         && WebRequestCommon.IsTracingEnabled(request))
        {
            // Get the time the HttpWebRequest was created
            // We save the start time here because we have no other way of getting it later
            // We've also already copied the headers to the underlying HttpClient, so adding it shouldn't
            // mean it gets sent either, it's just a storage area
            var unixTimespanMs = request.Headers.Get(HttpHeaderNames.InternalStartTime);

            var startTime = unixTimespanMs is not null && long.TryParse(unixTimespanMs, out var ms)
                                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                                : DateTimeOffset.UtcNow;

            // Check if any headers were injected by a previous call
            // Since it is possible for users to manually propagate headers (which we should
            // overwrite), check our cache which will be populated with header objects
            // that we have injected context into
            PropagationContext existingContext = default;
            if (HeadersInjectedCache.TryGetInjectedHeaders(request.Headers))
            {
                var headers = request.Headers.Wrap();

                // We are intentionally not merging any extracted baggage here into Baggage.Current:
                // We've already propagated baggage through the HTTP headers at this point,
                // and when this method is called this is presumably the "bottom" of the call chain,
                // and it may have been called on an entirely different thread.
                existingContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headers);
            }

            var existingSpanContext = existingContext.SpanContext;

            // If this operation creates the trace, then we need to re-apply the sampling priority
            bool setSamplingPriority = existingSpanContext?.SamplingPriority != null && Tracer.Instance.ActiveScope == null;

            Scope? scope = null;

            try
            {
                scope = ScopeFactory.CreateOutboundHttpScope(
                    Tracer.Instance,
                    request.Method,
                    request.RequestUri,
                    WebRequestCommon.IntegrationId,
                    out _,
                    traceId: existingSpanContext?.TraceId128 ?? TraceId.Zero,
                    spanId: existingSpanContext?.SpanId ?? 0,
                    startTime);

                if (scope is not null)
                {
                    if (setSamplingPriority)
                    {
                        scope.Span.Context.TraceContext.SetSamplingPriority(existingSpanContext!.SamplingPriority!.Value);
                    }

                    if (returnValue is HttpWebResponse response)
                    {
                        // scope.Span.SetHttpStatusCode((int)response.StatusCode, isServer: false, Tracer.Instance.CurrentTraceSettings.Settings);
                        scope.Dispose();
                    }
                    else if (exception is WebException { Status: WebExceptionStatus.ProtocolError, Response: HttpWebResponse exceptionResponse })
                    {
                        // Add the exception tags without setting the Error property
                        // SetHttpStatusCode will mark the span with an error if the StatusCode is within the configured range
                        // scope.Span.SetExceptionTags(exception);

                        // scope.Span.SetHttpStatusCode((int)exceptionResponse.StatusCode, isServer: false, Tracer.Instance.CurrentTraceSettings.Settings);
                        scope.Dispose();
                    }
                    else
                    {
                        scope.DisposeWithException(exception);
                    }
                }
            }
            catch
            {
                scope?.Dispose();
                throw;
            }
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
#endif
