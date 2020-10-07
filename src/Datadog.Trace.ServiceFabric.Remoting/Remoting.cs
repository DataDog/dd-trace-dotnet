using System;
using System.Globalization;
using System.Text;
using System.Threading;
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
        private const string IntegrationName = "ServiceRemoting";
        private const string SpanNamePrefix = "service-remoting";

        private static readonly Datadog.Trace.Vendors.Serilog.ILogger Log = Datadog.Trace.Logging.DatadogLogging.GetLogger(typeof(Remoting));

        private static int _enabled;

        /// <summary>
        /// Start tracing Service Remoting requests.
        /// </summary>
        public static void StartTracing()
        {
            if (Interlocked.CompareExchange(ref _enabled, 1, 0) == 0)
            {
                // client
                ServiceRemotingClientEvents.SendRequest += ServiceRemotingClientEvents_SendRequest;
                ServiceRemotingClientEvents.ReceiveResponse += ServiceRemotingClientEvents_ReceiveResponse;

                // server
                ServiceRemotingServiceEvents.ReceiveRequest += ServiceRemotingServiceEvents_ReceiveRequest;
                ServiceRemotingServiceEvents.SendResponse += ServiceRemotingServiceEvents_SendResponse;
            }
        }

        /// <summary>
        /// Stop tracing Service Remoting requests.
        /// </summary>
        public static void StopTracing()
        {
            if (Interlocked.CompareExchange(ref _enabled, 0, 1) == 1)
            {
                // client
                ServiceRemotingClientEvents.SendRequest -= ServiceRemotingClientEvents_SendRequest;
                ServiceRemotingClientEvents.ReceiveResponse -= ServiceRemotingClientEvents_ReceiveResponse;

                // server
                ServiceRemotingServiceEvents.ReceiveRequest -= ServiceRemotingServiceEvents_ReceiveRequest;
                ServiceRemotingServiceEvents.SendResponse -= ServiceRemotingServiceEvents_SendResponse;
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting client sends a request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void ServiceRemotingClientEvents_SendRequest(object? sender, EventArgs? e)
        {
            if (_enabled == 0)
            {
                return;
            }

            GetMessageHeaders(e, out var eventArgs, out var messageHeaders);

            var tracer = Tracer.Instance;
            var span = CreateSpan(tracer, context: null, SpanKinds.Client, eventArgs, messageHeaders, enableAnalyticsWithGlobalSetting: false);

            try
            {
                // inject propagation context into message headers for distributed tracing
                if (messageHeaders != null)
                {
                    string samplingPriorityTag = span.GetTag(Tags.SamplingPriority);
                    int? samplingPriority = int.TryParse(samplingPriorityTag, NumberStyles.None, CultureInfo.InvariantCulture, out var priority) ? priority : default;

                    var context = new PropagationContext
                                  {
                                      TraceId = span.TraceId,
                                      ParentSpanId = span.SpanId,
                                      SamplingPriority = samplingPriority,
                                      Origin = span.GetTag(Tags.Origin)
                                  };

                    InjectContext(context, messageHeaders);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error injecting message headers.");
            }

            tracer.ActivateSpan(span);
        }

        /// <summary>
        /// Event handler called when the Service Remoting client receives a response
        /// from the server after it finishes processing a request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void ServiceRemotingClientEvents_ReceiveResponse(object? sender, EventArgs e)
        {
            if (_enabled == 0)
            {
                return;
            }

            // var successfulResponseArg = e as ServiceRemotingResponseEventArgs;
            // var failedResponseArg = e as ServiceRemotingFailedResponseEventArgs;
            Scope? scope = null;

            try
            {
                scope = Tracer.Instance.ActiveScope;

                if (scope != null &&
                    e is ServiceRemotingFailedResponseEventArgs failedResponseArg &&
                    failedResponseArg.Error != null)
                {
                    scope.Span?.SetException(failedResponseArg.Error);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error accessing active scope or setting error tags.");
            }
            finally
            {
                scope?.Dispose();
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting server receives an incoming request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void ServiceRemotingServiceEvents_ReceiveRequest(object? sender, EventArgs e)
        {
            if (_enabled == 0)
            {
                return;
            }

            GetMessageHeaders(e, out var eventArgs, out var messageHeaders);
            PropagationContext propagationContext = default;
            SpanContext? spanContext = null;

            // extract propagation context from message headers for distributed tracing
            if (messageHeaders != null)
            {
                propagationContext = ExtractContext(messageHeaders);

                if (propagationContext.TraceId > 0 && propagationContext.ParentSpanId > 0)
                {
                    spanContext = new SpanContext(propagationContext.TraceId, propagationContext.ParentSpanId, (SamplingPriority?)propagationContext.SamplingPriority);
                }
            }

            var tracer = Tracer.Instance;
            var span = CreateSpan(tracer, spanContext, SpanKinds.Server, eventArgs, messageHeaders, enableAnalyticsWithGlobalSetting: true);

            if (!string.IsNullOrEmpty(propagationContext.Origin))
            {
                span.SetTag(Tags.Origin, propagationContext.Origin);
            }

            tracer.ActivateSpan(span);
        }

        /// <summary>
        /// Event handler called when the Service Remoting server sends a response
        /// after processing an incoming request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void ServiceRemotingServiceEvents_SendResponse(object? sender, EventArgs e)
        {
            if (_enabled == 0)
            {
                return;
            }

            // var successfulResponseArg = e as ServiceRemotingResponseEventArgs;
            // var failedResponseArg = e as ServiceRemotingFailedResponseEventArgs;

            var scope = Tracer.Instance.ActiveScope;

            if (scope != null)
            {
                if (e is ServiceRemotingFailedResponseEventArgs failedResponseArg && failedResponseArg.Error != null)
                {
                    scope.Span?.SetException(failedResponseArg.Error);
                }

                scope.Dispose();
            }
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
                if (!messageHeaders.TryGetHeaderValue(HttpHeaderNames.TraceId, out _))
                {
                    messageHeaders.AddHeader(HttpHeaderNames.TraceId, BitConverter.GetBytes(context.TraceId));
                }

                if (!messageHeaders.TryGetHeaderValue(HttpHeaderNames.ParentId, out _))
                {
                    messageHeaders.AddHeader(HttpHeaderNames.ParentId, BitConverter.GetBytes(context.ParentSpanId));
                }

                if (context.SamplingPriority != null &&
                    !messageHeaders.TryGetHeaderValue(HttpHeaderNames.SamplingPriority, out _))
                {
                    messageHeaders.AddHeader(HttpHeaderNames.SamplingPriority, BitConverter.GetBytes(context.SamplingPriority.Value));
                }

                if (!string.IsNullOrEmpty(context.Origin) &&
                    !messageHeaders.TryGetHeaderValue(HttpHeaderNames.Origin, out _))
                {
                    messageHeaders.AddHeader(HttpHeaderNames.Origin, Encoding.UTF8.GetBytes(context.Origin));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error injecting message headers.");
            }
        }

        private static PropagationContext ExtractContext(IServiceRemotingRequestMessageHeader messageHeaders)
        {
            try
            {
                PropagationContext propagationContext = default;

                if (messageHeaders.TryGetHeaderValue(HttpHeaderNames.TraceId, out byte[] traceIdBytes) &&
                    traceIdBytes?.Length == sizeof(ulong))
                {
                    propagationContext.TraceId = BitConverter.ToUInt64(traceIdBytes, 0);
                }

                if (messageHeaders.TryGetHeaderValue(HttpHeaderNames.ParentId, out byte[] parentIdBytes) &&
                    parentIdBytes?.Length == sizeof(ulong))
                {
                    propagationContext.ParentSpanId = BitConverter.ToUInt64(parentIdBytes, 0);
                }

                if (messageHeaders.TryGetHeaderValue(HttpHeaderNames.SamplingPriority, out byte[] samplingPriorityBytes) &&
                    samplingPriorityBytes?.Length == sizeof(int))
                {
                    propagationContext.SamplingPriority = BitConverter.ToInt32(samplingPriorityBytes, 0);
                }

                if (messageHeaders.TryGetHeaderValue(HttpHeaderNames.Origin, out byte[] originBytes) &&
                    originBytes?.Length > 0)
                {
                    propagationContext.Origin = Encoding.UTF8.GetString(originBytes);
                }

                return propagationContext;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting message headers.");
                return default;
            }
        }

        private static Span CreateSpan(
            Tracer tracer,
            SpanContext? context,
            string spanKind,
            ServiceRemotingRequestEventArgs? eventArgs,
            IServiceRemotingRequestMessageHeader? messageHeader,
            bool enableAnalyticsWithGlobalSetting)
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

            Span span = tracer.StartSpan($"{SpanNamePrefix}.{spanKind}", context);
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

            double? analyticsSampleRate = GetAnalyticsSampleRate(tracer, enableAnalyticsWithGlobalSetting);

            if (analyticsSampleRate != null)
            {
                span.SetTag(Tags.Analytics, analyticsSampleRate.Value.ToString(CultureInfo.InvariantCulture));
            }

            return span;
        }

        private static double? GetAnalyticsSampleRate(Tracer tracer, bool enabledWithGlobalSetting)
        {
            var integrationSettings = tracer.Settings.Integrations[IntegrationName];
            var analyticsEnabled = integrationSettings.AnalyticsEnabled ?? (enabledWithGlobalSetting && tracer.Settings.AnalyticsEnabled);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRate : (double?)null;
        }
    }
}
