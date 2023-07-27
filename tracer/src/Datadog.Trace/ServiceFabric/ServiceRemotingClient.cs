// <copyright file="ServiceRemotingClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Propagators;

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
                if (ServiceRemotingHelpers.AddEventHandler(ServiceRemotingConstants.ClientEventsTypeName, ServiceRemotingConstants.SendRequestEventName, ServiceRemotingClientEvents_SendRequest) &&
                    ServiceRemotingHelpers.AddEventHandler(ServiceRemotingConstants.ClientEventsTypeName, ServiceRemotingConstants.ReceiveResponseEventName, ServiceRemotingClientEvents_ReceiveResponse))
                {
                    // don't handle any client events until we have subscribed to both of them
                    _initialized = true;
                    Log.Debug($"Subscribed to {ServiceRemotingConstants.ClientEventsTypeName} events.");
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
            var tracer = Tracer.Instance;

            if (!_initialized || !tracer.Settings.IsIntegrationEnabled(ServiceRemotingConstants.IntegrationId))
            {
                return;
            }

            ServiceRemotingHelpers.GetMessageHeaders(e, out var eventArgs, out var messageHeaders);

            try
            {
                var tags = tracer.CurrentTraceSettings.Schema.Client.CreateServiceRemotingClientTags();
                var span = ServiceRemotingHelpers.CreateSpan(tracer, context: null, tags, eventArgs, messageHeaders);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

                try
                {
                    // inject propagation context into message headers for distributed tracing
                    if (messageHeaders != null)
                    {
                        SpanContextPropagator.Instance.Inject(
                            span.Context,
                            messageHeaders,
                            default(ServiceRemotingRequestMessageHeaderSetter));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error injecting span context into Service Fabric Service Remoting message headers.");
                }

                tracer.ActivateSpan(span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or activating Service Fabric Service Remoting client span.");
            }
        }

        /// <summary>
        /// Event handler called when the Service Remoting client receives a response
        /// from the server after it finishes processing a request.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">The event arguments. Can be of type <c>IServiceRemotingResponseEventArgs2</c> on success
        /// or <c>IServiceRemotingFailedResponseEventArgs</c> on failure.</param>
        private static void ServiceRemotingClientEvents_ReceiveResponse(object? sender, EventArgs? e)
        {
            if (!_initialized || !Tracer.Instance.Settings.IsIntegrationEnabled(ServiceRemotingConstants.IntegrationId))
            {
                return;
            }

            ServiceRemotingHelpers.FinishSpan(e, SpanKinds.Client);
        }

        private readonly struct ServiceRemotingRequestMessageHeaderSetter : ICarrierSetter<IServiceRemotingRequestMessageHeader>
        {
            public void Set(IServiceRemotingRequestMessageHeader carrier, string key, string value)
            {
                carrier.TryAddHeader(key, value);
            }
        }
    }
}
