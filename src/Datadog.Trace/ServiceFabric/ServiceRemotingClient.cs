#nullable enable

using System;
using System.Text;
using System.Threading;

namespace Datadog.Trace.ServiceFabric
{
    /// <summary>
    /// Provides tracing of ServiceRemotingClientEvents.
    /// </summary>
    internal static class ServiceRemotingClient
    {
        private static readonly Logging.IDatadogLogger Log = Logging.DatadogLogging.GetLoggerFor(typeof(ServiceRemotingClient));

        private static int _firstInitialization = 1;
        private static bool _initialized;

        /// <summary>
        /// Start tracing ServiceRemotingClientEvents.
        /// </summary>
        public static void StartTracing()
        {
            // only run this code once
            if (Interlocked.Exchange(ref _firstInitialization, 0) == 1)
            {
                // try to subscribe to client events
                if (ServiceRemotingHelpers.AddEventHandler(ServiceRemotingHelpers.ClientEventsTypeName, ServiceRemotingHelpers.SendRequestEventName, ServiceRemotingClientEvents_SendRequest) &&
                    ServiceRemotingHelpers.AddEventHandler(ServiceRemotingHelpers.ClientEventsTypeName, ServiceRemotingHelpers.ReceiveResponseEventName, ServiceRemotingClientEvents_ReceiveResponse))
                {
                    // don't handle any client events until we have subscribed to both of them
                    _initialized = true;
                    Log.Debug($"Subscribed to {ServiceRemotingHelpers.ClientEventsTypeName} events.");
                }
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting client sends a request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        private static void ServiceRemotingClientEvents_SendRequest(object? sender, EventArgs? e)
        {
            if (!_initialized || !Tracer.Instance.Settings.IsIntegrationEnabled(ServiceRemotingHelpers.IntegrationId))
            {
                return;
            }

            ServiceRemotingHelpers.GetMessageHeaders(e, out var eventArgs, out var messageHeaders);

            try
            {
                var tracer = Tracer.Instance;
                var span = ServiceRemotingHelpers.CreateSpan(tracer, context: null, SpanKinds.Client, eventArgs, messageHeaders);

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
                    Log.Error(ex, "Error injecting Service Fabric Service Remoting message headers.");
                }

                tracer.ActivateSpan(span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or activating Service Fabric Service Remoting span.");
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting client receives a response
        /// from the server after it finishes processing a request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments. Can be of type <see cref="IServiceRemotingResponseEventArgs"/> on success
        /// or <see cref="IServiceRemotingFailedResponseEventArgs"/> on failure.</param>
        private static void ServiceRemotingClientEvents_ReceiveResponse(object? sender, EventArgs? e)
        {
            if (!_initialized || !Tracer.Instance.Settings.IsIntegrationEnabled(ServiceRemotingHelpers.IntegrationId))
            {
                return;
            }

            ServiceRemotingHelpers.FinishSpan(e, SpanKinds.Client);
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
                Log.Error(ex, "Error injecting Service Fabric Service Remoting message headers.");
            }
        }
    }
}
