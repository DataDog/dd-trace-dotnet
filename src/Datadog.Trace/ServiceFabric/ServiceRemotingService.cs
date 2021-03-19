#nullable enable

using System;
using System.Collections.Generic;
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
                if (ServiceRemotingHelpers.AddEventHandler(ServiceRemotingConstants.ServiceEventsTypeName, ServiceRemotingConstants.ReceiveRequestEventName, ServiceRemotingServiceEvents_ReceiveRequest) &&
                    ServiceRemotingHelpers.AddEventHandler(ServiceRemotingConstants.ServiceEventsTypeName, ServiceRemotingConstants.SendResponseEventName, ServiceRemotingServiceEvents_SendResponse))
                {
                    // don't handle any service events until we have subscribed to both of them
                    _initialized = true;
                    Log.Debug($"Subscribed to {ServiceRemotingConstants.ServiceEventsTypeName} events.");
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

            if (!_initialized || !tracer.Settings.IsIntegrationEnabled(ServiceRemotingConstants.IntegrationId))
            {
                return;
            }

            ServiceRemotingHelpers.GetMessageHeaders(e, out var eventArgs, out var messageHeaders);
            SpanContext? spanContext = null;

            try
            {
                // extract propagation context from message headers for distributed tracing
                if (messageHeaders != null)
                {
                    spanContext = SpanContextPropagator.Instance.Extract(messageHeaders, GetHeaders);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting span context from Service Fabric Service Remoting message headers.");
            }

            try
            {
                var span = ServiceRemotingHelpers.CreateSpan(tracer, spanContext, SpanKinds.Server, eventArgs, messageHeaders);
                tracer.ActivateSpan(span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or activating new Service Fabric Service Remoting service span.");
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting server sends a response
        /// after processing an incoming request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments. Can be of type <c>IServiceRemotingResponseEventArgs2</c> on success
        /// or <c>IServiceRemotingFailedResponseEventArgs2</c> on failure.</param>
        private static void ServiceRemotingServiceEvents_SendResponse(object? sender, EventArgs? e)
        {
            if (!_initialized || !Tracer.Instance.Settings.IsIntegrationEnabled(ServiceRemotingConstants.IntegrationId))
            {
                return;
            }

            ServiceRemotingHelpers.FinishSpan(e, SpanKinds.Server);
        }

        private static IEnumerable<string?> GetHeaders(IServiceRemotingRequestMessageHeader headers, string headerName)
        {
            if (headers.TryGetHeaderValueString(headerName, out var headerValue))
            {
                yield return headerValue;
            }
        }
    }
}
