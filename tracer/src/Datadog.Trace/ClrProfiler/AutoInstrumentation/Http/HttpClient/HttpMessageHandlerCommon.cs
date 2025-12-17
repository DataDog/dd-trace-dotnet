// <copyright file="HttpMessageHandlerCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring.TransactionTracking;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient
{
    internal static class HttpMessageHandlerCommon
    {
        public static CallTargetState OnMethodBegin<TTarget, TRequest>(TTarget instance, TRequest requestMessage, CancellationToken cancellationToken, IntegrationId integrationId, IntegrationId? implementationIntegrationId)
            where TRequest : IHttpRequestMessage
        {
            if (requestMessage.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var tracer = Tracer.Instance;
            var headers = requestMessage.Headers;

            if (IsTracingEnabled(tracer, headers, implementationIntegrationId))
            {
                var scope = ScopeFactory.CreateOutboundHttpScope(
                    tracer,
                    requestMessage.Method.Method,
                    requestMessage.RequestUri,
                    integrationId,
                    out var tags);

                if (scope is not null)
                {
                    tags.HttpClientHandlerType = instance.GetType().FullName;

                    // add propagation headers to the HTTP request
                    var context = new PropagationContext(scope.Span.Context, Baggage.Current);
                    tracer.TracerManager.SpanContextPropagator.Inject(context, new HttpHeadersCollection(headers));

                    tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(implementationIntegrationId ?? integrationId);
                    return new CallTargetState(scope);
                }

                Console.WriteLine(@"### Handling outgoing http request");
                var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
                if (dataStreamsManager.IsEnabled)
                {
                    var extractors = dataStreamsManager.GetExtractorsByType(DataStreamsTransactionExtractor.Type.HttpOutHeaders);
                    Console.WriteLine($@"### Found {extractors?.Count} extractors");
                    if (extractors != null)
                    {
                        foreach (var extractor in extractors)
                        {
                            Console.WriteLine($@"### Applying extractor named {extractor.Name} with value {extractor.Value}");
                            if (headers.TryGetValues(extractor.Value, out var headerValues))
                            {
                                foreach (var headerValue in headerValues)
                                {
                                    Console.WriteLine($@"### Extracted transaction {headerValue}");
                                    dataStreamsManager.TrackTransaction(headerValue, extractor.Name);
                                }
                            }
                        }
                    }
                }
            }

            return CallTargetState.GetDefault();
        }

        public static TResponse OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, in CallTargetState state)
            where TResponse : IHttpResponseMessage
        {
            Scope scope = state.Scope;

            if (scope is null)
            {
                return responseMessage;
            }

            try
            {
                if (responseMessage.Instance is not null)
                {
                    scope.Span.SetHttpStatusCode(responseMessage.StatusCode, false, Tracer.Instance.CurrentTraceSettings.Settings);
                }

                if (exception != null)
                {
                    scope.Span.SetException(exception);
                }
            }
            finally
            {
                scope.Dispose();
            }

            return responseMessage;
        }

        private static bool IsTracingEnabled(Tracer tracer, IRequestHeaders headers, IntegrationId? implementationIntegrationId)
        {
            if (implementationIntegrationId != null && !tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(implementationIntegrationId.Value))
            {
                return false;
            }

            if (headers.TryGetValues(HttpHeaderNames.TracingEnabled, out var headerValues))
            {
                if (headerValues is string[] arrayValues)
                {
                    for (var i = 0; i < arrayValues.Length; i++)
                    {
                        if (string.Equals(arrayValues[i], "false", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                if (headerValues != null && headerValues.Any(s => string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)))
                {
                    // tracing is disabled for this request via http header
                    return false;
                }
            }

            return true;
        }
    }
}
