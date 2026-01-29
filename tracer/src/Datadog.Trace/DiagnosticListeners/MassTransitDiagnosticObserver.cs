// <copyright file="MassTransitDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using System.Collections.Concurrent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Instruments MassTransit message bus operations via DiagnosticSource.
    /// <para/>
    /// MassTransit 7 emits DiagnosticSource events but uses the older Activity pattern
    /// (not ActivitySource), so these Activities are not picked up by our ActivityListener.
    /// We create our own Datadog spans based on the diagnostic events.
    /// </summary>
    /// <remarks>
    /// MassTransit emits the following diagnostic events:
    /// - MassTransit.Transport.Send (Start/Stop) - When messages are sent
    /// - MassTransit.Transport.Receive (Start/Stop) - When messages are received (NOT instrumented - too low-level)
    /// - MassTransit.Consumer.Consume (Start/Stop) - When a consumer processes a message
    /// - MassTransit.Consumer.Handle (Start/Stop) - When a handler processes a message (NOT instrumented - fires with Consume)
    /// <para/>
    /// We only instrument Send and Consume to avoid duplicate spans. The Receive event fires at the transport
    /// level before message deserialization, and Handle fires in addition to Consume for the same message.
    /// Context propagation (trace context injection/extraction) happens in Send and Consume handlers.
    /// </remarks>
    internal sealed class MassTransitDiagnosticObserver : DiagnosticObserver
    {
        private const string DiagnosticListenerName = "MassTransit";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MassTransitDiagnosticObserver>();

        // Store active scopes keyed by Activity.Id to match Start/Stop events
        private readonly ConcurrentDictionary<string, Scope> _activeScopes = new();

        protected override string ListenerName => DiagnosticListenerName;

        public override bool IsSubscriberEnabled()
        {
            var enabled = Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.MassTransit);
            Log.Debug("MassTransitDiagnosticObserver.IsSubscriberEnabled: {Enabled}", enabled);
            return enabled;
        }

        public override IDisposable? SubscribeIfMatch(System.Diagnostics.DiagnosticListener diagnosticListener)
        {
            Log.Debug("MassTransitDiagnosticObserver.SubscribeIfMatch: Checking listener '{ListenerName}'", diagnosticListener.Name);

            if (diagnosticListener.Name == ListenerName)
            {
                var subscription = diagnosticListener.Subscribe(this);
                Log.Debug("MassTransitDiagnosticObserver: Subscribed to '{ListenerName}'", diagnosticListener.Name);
                return subscription;
            }

            return null;
        }

        protected override void OnNext(string eventName, object arg)
        {
            Log.Debug(
                "MassTransitDiagnosticObserver.OnNext: Event='{EventName}', ArgType={ArgType}",
                eventName,
                arg?.GetType().FullName ?? "null");

            try
            {
                switch (eventName)
                {
                    // Send events (producer spans)
                    case "MassTransit.Transport.Send.Start":
                        OnSendStart(arg);
                        break;
                    case "MassTransit.Transport.Send.Stop":
                        OnStop("Send");
                        break;
                    case "MassTransit.Transport.Send.Exception":
                        OnException("Send", arg);
                        break;

                    // NOTE: We intentionally do NOT instrument Receive events.
                    // Receive fires at the transport level before message deserialization,
                    // which would create duplicate spans alongside Consume events.

                    // Consumer Consume events (consumer spans)
                    case "MassTransit.Consumer.Consume.Start":
                        OnConsumeStart(arg);
                        break;
                    case "MassTransit.Consumer.Consume.Stop":
                        OnStop("Consume");
                        break;
                    case "MassTransit.Consumer.Consume.Exception":
                        OnException("Consume", arg);
                        break;

                    // NOTE: We intentionally do NOT instrument Handle events.
                    // Handle fires in addition to Consume for the same message,
                    // which would create duplicate spans.

                    default:
                        Log.Debug("MassTransitDiagnosticObserver: Unhandled event '{EventName}'", eventName);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MassTransitDiagnosticObserver.OnNext: Error handling event '{EventName}'", eventName);
            }
        }

        private static string GetCurrentActivityId()
        {
            var activity = System.Diagnostics.Activity.Current;
            return activity?.Id ?? string.Empty;
        }

        private void OnSendStart(object? arg)
        {
            if (arg == null)
            {
                return;
            }

            var activityId = GetCurrentActivityId();

            // Extract destination and message type from the SendContext
            var destinationAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "DestinationAddress")?.ToString();
            var messageType = MassTransitCommon.GetMessageType(arg);

            Log.Debug(
                "MassTransitDiagnosticObserver.OnSendStart: Destination={Destination}, MessageType={MessageType}",
                destinationAddress,
                messageType);

            var scope = MassTransitCommon.CreateProducerScope(
                Tracer.Instance,
                MassTransitConstants.OperationSend,
                destinationAddress,
                messageType);

            if (scope != null && !string.IsNullOrEmpty(activityId))
            {
                StoreScope("Send", activityId, scope);

                // Set additional context tags
                var messageId = MassTransitCommon.TryGetProperty<Guid?>(arg, "MessageId");
                var conversationId = MassTransitCommon.TryGetProperty<Guid?>(arg, "ConversationId");
                var correlationId = MassTransitCommon.TryGetProperty<Guid?>(arg, "CorrelationId");
                MassTransitCommon.SetContextTags(scope, messageId, conversationId, correlationId);

                // Inject trace context into message headers for distributed tracing
                MassTransitCommon.InjectTraceContext(Tracer.Instance, arg, scope);

                Log.Debug(
                    "MassTransitDiagnosticObserver.OnSendStart: Created span TraceId={TraceId}, SpanId={SpanId}",
                    scope.Span?.TraceId,
                    scope.Span?.SpanId);
            }
        }

        private void OnConsumeStart(object? arg)
        {
            if (arg == null)
            {
                return;
            }

            var activityId = GetCurrentActivityId();

            // For consume, we get a ConsumeContext - try multiple address properties
            var destinationAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "DestinationAddress")?.ToString();

            // If not available, try ReceiveContext.InputAddress
            if (string.IsNullOrEmpty(destinationAddress))
            {
                var receiveContext = MassTransitCommon.TryGetProperty<object>(arg, "ReceiveContext");
                destinationAddress = MassTransitCommon.TryGetProperty<Uri>(receiveContext, "InputAddress")?.ToString();
            }

            // If still not available, try SourceAddress
            if (string.IsNullOrEmpty(destinationAddress))
            {
                destinationAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "SourceAddress")?.ToString();
            }

            var messageType = MassTransitCommon.GetMessageType(arg);

            Log.Debug(
                "MassTransitDiagnosticObserver.OnConsumeStart: Destination={Destination}, MessageType={MessageType}",
                destinationAddress,
                messageType);

            // Extract parent context from headers for distributed tracing
            var parentContext = MassTransitCommon.ExtractTraceContext(Tracer.Instance, arg);

            var scope = MassTransitCommon.CreateConsumerScope(
                Tracer.Instance,
                MassTransitConstants.OperationProcess,
                destinationAddress,
                messageType,
                parentContext);

            if (scope != null && !string.IsNullOrEmpty(activityId))
            {
                StoreScope("Consume", activityId, scope);

                // Set additional context tags
                var messageId = MassTransitCommon.TryGetProperty<Guid?>(arg, "MessageId");
                var conversationId = MassTransitCommon.TryGetProperty<Guid?>(arg, "ConversationId");
                var correlationId = MassTransitCommon.TryGetProperty<Guid?>(arg, "CorrelationId");
                MassTransitCommon.SetContextTags(scope, messageId, conversationId, correlationId);

                Log.Debug(
                    "MassTransitDiagnosticObserver.OnConsumeStart: Created span TraceId={TraceId}, SpanId={SpanId}",
                    scope.Span?.TraceId,
                    scope.Span?.SpanId);
            }
        }

        private void OnStop(string operationType)
        {
            var activityId = GetCurrentActivityId();

            if (string.IsNullOrEmpty(activityId))
            {
                Log.Debug("MassTransitDiagnosticObserver.OnStop: No activity ID for {OperationType}", operationType);
                return;
            }

            var key = $"{operationType}:{activityId}";
            if (_activeScopes.TryRemove(key, out var scope))
            {
                MassTransitCommon.CloseScope(scope, operationType);
                Log.Debug("MassTransitDiagnosticObserver.OnStop: Closed scope for key '{Key}'", key);
            }
            else
            {
                Log.Debug(
                    "MassTransitDiagnosticObserver.OnStop: No scope found for key '{Key}'",
                    key);
            }
        }

        private void OnException(string operationType, object? arg)
        {
            var activityId = GetCurrentActivityId();

            if (string.IsNullOrEmpty(activityId))
            {
                return;
            }

            var key = $"{operationType}:{activityId}";
            if (_activeScopes.TryGetValue(key, out var scope))
            {
                var exception = arg as Exception ?? MassTransitCommon.TryGetProperty<Exception>(arg, "Exception");
                MassTransitCommon.SetException(scope, exception);
                Log.Debug("MassTransitDiagnosticObserver.OnException: Set error on scope for key '{Key}'", key);
            }
        }

        private void StoreScope(string operationType, string activityId, Scope scope)
        {
            var key = $"{operationType}:{activityId}";
            _activeScopes[key] = scope;
            Log.Debug("MassTransitDiagnosticObserver.StoreScope: Stored scope with key '{Key}'", key);
        }
    }
}
#endif
