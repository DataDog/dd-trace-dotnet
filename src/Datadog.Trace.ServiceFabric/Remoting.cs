using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Datadog.Trace.Configuration;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;

namespace Datadog.Trace.ServiceFabric
{
    /// <summary>
    /// Provides methods used start and stop tracing Service Remoting requests.
    /// </summary>
    public static class Remoting
    {
        private const string SpanNamePrefix = "service-remoting";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.ServiceRemoting));

        // ILogger and DatadogLogging are internal to Datadog.Trade.dll, so we use NuGet package IgnoresAccessChecksToGenerator
        // to generate [IgnoresAccessChecksToAttribute] and generate reference assemblies where they are public
        private static readonly Datadog.Trace.Logging.IDatadogLogger Log = Datadog.Trace.Logging.DatadogLogging.GetLoggerFor(typeof(Remoting));

        private static int _firstInitialization = 1;
        private static bool _initialized;
        private static string? _clientAnalyticsSampleRate;
        private static string? _serverAnalyticsSampleRate;

        /// <summary>
        /// Start tracing Service Remoting requests.
        /// </summary>
        public static void StartTracing()
        {
            // only run this code once
            if (Interlocked.Exchange(ref _firstInitialization, 0) == 1)
            {
                // cache settings
                _clientAnalyticsSampleRate = GetAnalyticsSampleRate(Tracer.Instance, enabledWithGlobalSetting: false)?.ToString(CultureInfo.InvariantCulture);
                _serverAnalyticsSampleRate = GetAnalyticsSampleRate(Tracer.Instance, enabledWithGlobalSetting: true)?.ToString(CultureInfo.InvariantCulture);

                // client events
                ServiceRemotingClientEvents.SendRequest += ServiceRemotingClientEvents_SendRequest;
                ServiceRemotingClientEvents.ReceiveResponse += ServiceRemotingClientEvents_ReceiveResponse;

                // server events
                ServiceRemotingServiceEvents.ReceiveRequest += ServiceRemotingServiceEvents_ReceiveRequest;
                ServiceRemotingServiceEvents.SendResponse += ServiceRemotingServiceEvents_SendResponse;

                // don't handle any events until we have subscribed to all of them
                _initialized = true;
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting client sends a request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void ServiceRemotingClientEvents_SendRequest(object? sender, EventArgs? e)
        {
            if (!_initialized)
            {
                return;
            }

            GetMessageHeaders(e, out var eventArgs, out var messageHeaders);

            try
            {
                var tracer = Tracer.Instance;
                var span = CreateSpan(tracer, context: null, SpanKinds.Client, eventArgs, messageHeaders);

                try
                {
                    // inject propagation context into message headers for distributed tracing
                    if (messageHeaders != null)
                    {
                        SamplingPriority? samplingPriority = span.Context.TraceContext?.SamplingPriority ?? span.Context.SamplingPriority;
                        string? origin = span.GetTag(Tags.Origin);
                        var context = new PropagationContext(span.TraceId, span.SpanId, samplingPriority, origin);

                        InjectContext(context, messageHeaders);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error injecting message headers.");
                }

                tracer.ActivateSpan(span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or activating span.");
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting client receives a response
        /// from the server after it finishes processing a request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments. Can be of type <see cref="ServiceRemotingResponseEventArgs"/> on success
        /// or <see cref="ServiceRemotingFailedResponseEventArgs"/> on failure.</param>
        private static void ServiceRemotingClientEvents_ReceiveResponse(object? sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            FinishSpan(e, SpanKinds.Client);
        }

        /// <summary>
        /// Event handler called when the Service Remoting server receives an incoming request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void ServiceRemotingServiceEvents_ReceiveRequest(object? sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            GetMessageHeaders(e, out var eventArgs, out var messageHeaders);
            PropagationContext? propagationContext = null;
            SpanContext? spanContext = null;

            try
            {
                // extract propagation context from message headers for distributed tracing
                if (messageHeaders != null)
                {
                    propagationContext = ExtractContext(messageHeaders);

                    if (propagationContext != null)
                    {
                        spanContext = new SpanContext(propagationContext.Value.TraceId, propagationContext.Value.ParentSpanId, propagationContext.Value.SamplingPriority);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error using propagation context to initialize span context.");
            }

            try
            {
                var tracer = Tracer.Instance;
                var span = CreateSpan(tracer, spanContext, SpanKinds.Server, eventArgs, messageHeaders);

                try
                {
                    string? origin = propagationContext?.Origin;

                    if (!string.IsNullOrEmpty(origin))
                    {
                        span.SetTag(Tags.Origin, origin);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error setting origin tag on span.");
                }

                tracer.ActivateSpan(span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or activating new span.");
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting server sends a response
        /// after processing an incoming request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments. Can be of type <see cref="ServiceRemotingResponseEventArgs"/> on success
        /// or <see cref="ServiceRemotingFailedResponseEventArgs"/> on failure.</param>
        private static void ServiceRemotingServiceEvents_SendResponse(object? sender, EventArgs e)
        {
            if (!_initialized)
            {
                return;
            }

            FinishSpan(e, SpanKinds.Server);
        }

        private static void GetMessageHeaders(EventArgs? eventArgs, out ServiceRemotingRequestEventArgs? requestEventArgs, out IServiceRemotingRequestMessageHeader? messageHeaders)
        {
            requestEventArgs = eventArgs as ServiceRemotingRequestEventArgs;

            try
            {
                if (requestEventArgs == null)
                {
                    Log.Warning("Unexpected EventArgs type: {0}", eventArgs?.GetType().FullName ?? "null");
                }

                messageHeaders = requestEventArgs?.Request?.GetHeader();

                if (messageHeaders == null)
                {
                    Log.Warning("Cannot access request headers.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error accessing request headers.");
                messageHeaders = null;
            }
        }

        private static void InjectContext(PropagationContext context, IServiceRemotingRequestMessageHeader messageHeaders)
        {
            if (context.TraceId == 0 || context.ParentSpanId == 0)
            {
                return;
            }

            try
            {
                messageHeaders.TryAddHeader(HttpHeaderNames.TraceId, context, ctx => BitConverter.GetBytes(ctx.TraceId));

                messageHeaders.TryAddHeader(HttpHeaderNames.ParentId, context, ctx => BitConverter.GetBytes(ctx.ParentSpanId));

                if (context.SamplingPriority != null)
                {
                    messageHeaders.TryAddHeader(HttpHeaderNames.SamplingPriority, context, ctx => BitConverter.GetBytes((int)ctx.SamplingPriority!));
                }

                if (!string.IsNullOrEmpty(context.Origin))
                {
                    messageHeaders.TryAddHeader(HttpHeaderNames.Origin, context, ctx => Encoding.UTF8.GetBytes(ctx.Origin!));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error injecting message headers.");
            }
        }

        private static PropagationContext? ExtractContext(IServiceRemotingRequestMessageHeader messageHeaders)
        {
            try
            {
                ulong traceId = messageHeaders.TryGetHeaderValueUInt64(HttpHeaderNames.TraceId) ?? 0;

                if (traceId > 0)
                {
                    ulong parentSpanId = messageHeaders.TryGetHeaderValueUInt64(HttpHeaderNames.ParentId) ?? 0;

                    if (parentSpanId > 0)
                    {
                        SamplingPriority? samplingPriority = (SamplingPriority?)messageHeaders.TryGetHeaderValueInt32(HttpHeaderNames.SamplingPriority);
                        string? origin = messageHeaders.TryGetHeaderValueString(HttpHeaderNames.Origin);

                        return new PropagationContext(traceId, parentSpanId, samplingPriority, origin);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting message headers.");
                return default;
            }
        }

        private static string GetSpanName(string spanKind)
        {
            return $"{SpanNamePrefix}.{spanKind}";
        }

        private static Span CreateSpan(
            Tracer tracer,
            SpanContext? context,
            string spanKind,
            ServiceRemotingRequestEventArgs? eventArgs,
            IServiceRemotingRequestMessageHeader? messageHeader)
        {
            string? methodName = null;
            string? resourceName = null;
            string? serviceUrl = eventArgs?.ServiceUri?.AbsoluteUri;

            if (eventArgs != null)
            {
                methodName = eventArgs.MethodName;

                if (string.IsNullOrEmpty(methodName))
                {
                    // use the numeric id as the method name
                    methodName = messageHeader == null ? "unknown" : messageHeader.MethodId.ToString(CultureInfo.InvariantCulture);
                }

                resourceName = serviceUrl == null ? methodName : $"{serviceUrl}/{methodName}";
            }

            Span span = tracer.StartSpan(GetSpanName(spanKind), context);
            span.ResourceName = resourceName ?? "unknown";
            span.SetTag(Tags.SpanKind, spanKind);

            if (serviceUrl != null)
            {
                span.SetTag(Tags.HttpUrl, serviceUrl);
            }

            if (methodName != null)
            {
                span.SetTag("method-name", methodName);
            }

            if (messageHeader != null)
            {
                span.SetTag("method-id", messageHeader.MethodId.ToString(CultureInfo.InvariantCulture));
                span.SetTag("interface-id", messageHeader.InterfaceId.ToString(CultureInfo.InvariantCulture));

                if (messageHeader.InvocationId != null)
                {
                    span.SetTag("invocation-id", messageHeader.InvocationId);
                }
            }

            switch (spanKind)
            {
                case SpanKinds.Client when _clientAnalyticsSampleRate != null:
                    span.SetTag(Tags.Analytics, _clientAnalyticsSampleRate);
                    break;
                case SpanKinds.Server when _serverAnalyticsSampleRate != null:
                    span.SetTag(Tags.Analytics, _serverAnalyticsSampleRate);
                    break;
            }

            return span;
        }

        private static void FinishSpan(EventArgs e, string spanKind)
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                var scope = Tracer.Instance.ActiveScope;

                if (scope == null)
                {
                    Log.Warning("Expected an active scope, but there is none.");
                    return;
                }

                string expectedSpanName = GetSpanName(spanKind);

                if (expectedSpanName != scope.Span.OperationName)
                {
                    Log.Warning("Expected span name {0}, but found {1} instead.", expectedSpanName, scope.Span.OperationName);
                    return;
                }

                try
                {
                    if (e is ServiceRemotingFailedResponseEventArgs failedResponseArg && failedResponseArg.Error != null)
                    {
                        scope.Span?.SetException(failedResponseArg.Error);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error setting exception tags on span.");
                }

                scope.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error accessing or finishing active span.");
            }
        }

        private static double? GetAnalyticsSampleRate(Tracer tracer, bool enabledWithGlobalSetting)
        {
            IntegrationSettings integrationSettings = tracer.Settings.Integrations[IntegrationId];
            bool analyticsEnabled = integrationSettings.AnalyticsEnabled ?? (enabledWithGlobalSetting && tracer.Settings.AnalyticsEnabled);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRate : (double?)null;
        }
    }
}
