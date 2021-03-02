#nullable enable

using System;
using System.Threading;

namespace Datadog.Trace.ServiceFabric
{
    /// <summary>
    /// Provides tracing of ServiceRemotingServiceEvents.
    /// </summary>
    internal static class ServiceRemotingService
    {
        private static readonly Logging.IDatadogLogger Log = Logging.DatadogLogging.GetLoggerFor(typeof(ServiceRemotingService));

        private static int _firstInitialization = 1;
        private static bool _initialized;

        /// <summary>
        /// Start tracing ServiceRemotingServiceEvents.
        /// </summary>
        public static void StartTracing()
        {
            // only run this code once
            if (Interlocked.Exchange(ref _firstInitialization, 0) == 1)
            {
                // try to subscribe to service events
                if (ServiceRemotingHelpers.AddEventHandler(ServiceRemotingHelpers.ServiceEventsTypeName, ServiceRemotingHelpers.ReceiveRequestEventName, ServiceRemotingServiceEvents_ReceiveRequest) &&
                    ServiceRemotingHelpers.AddEventHandler(ServiceRemotingHelpers.ServiceEventsTypeName, ServiceRemotingHelpers.SendResponseEventName, ServiceRemotingServiceEvents_SendResponse))
                {
                    // don't handle any service events until we have subscribed to both of them
                    _initialized = true;
                    Log.Debug($"Subscribed to {ServiceRemotingHelpers.ServiceEventsTypeName} events.");
                }
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting server receives an incoming request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void ServiceRemotingServiceEvents_ReceiveRequest(object? sender, EventArgs? e)
        {
            var tracer = Tracer.Instance;

            if (!_initialized || !tracer.Settings.IsIntegrationEnabled(ServiceRemotingHelpers.IntegrationId))
            {
                return;
            }

            ServiceRemotingHelpers.GetMessageHeaders(e, out var eventArgs, out var messageHeaders);
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
                Log.Error(ex, "Error using propagation context to initialize Service Fabric Service Remoting span context.");
            }

            try
            {
                var span = ServiceRemotingHelpers.CreateSpan(tracer, spanContext, SpanKinds.Server, eventArgs, messageHeaders);

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
                    Log.Error(ex, "Error setting origin tag on Service Fabric Service Remoting span.");
                }

                tracer.ActivateSpan(span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or activating new Service Fabric Service Remoting span.");
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting server sends a response
        /// after processing an incoming request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments. Can be of type <see cref="IServiceRemotingResponseEventArgs"/> on success
        /// or <see cref="IServiceRemotingFailedResponseEventArgs"/> on failure.</param>
        private static void ServiceRemotingServiceEvents_SendResponse(object? sender, EventArgs? e)
        {
            if (!_initialized || !Tracer.Instance.Settings.IsIntegrationEnabled(ServiceRemotingHelpers.IntegrationId))
            {
                return;
            }

            ServiceRemotingHelpers.FinishSpan(e, SpanKinds.Server);
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
                Log.Error(ex, "Error extracting Service Fabric Service Remoting message headers.");
                return default;
            }
        }
    }
}
