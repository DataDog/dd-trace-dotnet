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
    /// - MassTransit.Consumer.Handle (Start/Stop) - When a handler processes a message
    /// - MassTransit.Saga.Send (Start/Stop) - When a saga receives a message
    /// - MassTransit.Saga.RaiseEvent (Start/Stop) - When a saga raises an event
    /// - MassTransit.Saga.SendQuery (Start/Stop) - When a saga handles a query
    /// - MassTransit.Saga.Initiate (Start/Stop) - When a saga is initiated
    /// - MassTransit.Saga.Orchestrate (Start/Stop) - When a saga orchestrates
    /// - MassTransit.Saga.Observe (Start/Stop) - When a saga observes
    /// - MassTransit.Activity.Execute (Start/Stop) - When a Routing Slip activity executes
    /// - MassTransit.Activity.Compensate (Start/Stop) - When a Routing Slip activity compensates
    /// <para/>
    /// We instrument all events except Receive. The Receive event fires at the transport
    /// level before message deserialization, which would create duplicate spans alongside consumer events.
    /// Context propagation (trace context injection/extraction) happens in Send and Consume handlers.
    /// <para/>
    /// NOTE: MassTransit 7 does NOT emit exception information through DiagnosticSource events (Stop event arg
    /// is always null). To capture exceptions, we use CallTarget instrumentation on BaseReceiveContext.NotifyFaulted
    /// which stores the exception keyed by Activity.TraceId. We use TraceId instead of Activity.Id because
    /// NotifyFaulted may be called from a child activity (Handle/Saga) while the Stop event fires on the parent
    /// activity (Consume). The OnStop handler retrieves this exception and marks the span as an error.
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

                    // NOTE: We intentionally do NOT instrument Receive events.
                    // Receive fires at the transport level before message deserialization,
                    // which would create duplicate spans alongside Consume events.

                    // Consumer Consume events (consumer spans)
                    case "MassTransit.Consumer.Consume.Start":
                        OnConsumeStart(arg, "Consume");
                        break;
                    case "MassTransit.Consumer.Consume.Stop":
                        OnStop("Consume");
                        break;

                    // Handler events (for message handlers registered via Handler<T>)
                    // These fire INSTEAD OF Consume events when using handlers
                    case "MassTransit.Consumer.Handle.Start":
                        OnConsumeStart(arg, "Handle");
                        break;
                    case "MassTransit.Consumer.Handle.Stop":
                        OnStop("Handle");
                        break;

                    // Saga events (for state machine sagas)
                    // Saga.Send fires when the saga receives a message
                    case "MassTransit.Saga.Send.Start":
                        OnConsumeStart(arg, "SagaSend");
                        break;
                    case "MassTransit.Saga.Send.Stop":
                        OnStop("SagaSend");
                        break;

                    // Saga.RaiseEvent fires when a saga state machine raises an event
                    case "MassTransit.Saga.RaiseEvent.Start":
                        OnConsumeStart(arg, "SagaRaiseEvent");
                        break;
                    case "MassTransit.Saga.RaiseEvent.Stop":
                        OnStop("SagaRaiseEvent");
                        break;

                    // Additional Saga events
                    case "MassTransit.Saga.SendQuery.Start":
                        OnConsumeStart(arg, "SagaSendQuery");
                        break;
                    case "MassTransit.Saga.SendQuery.Stop":
                        OnStop("SagaSendQuery");
                        break;
                    case "MassTransit.Saga.Initiate.Start":
                        OnConsumeStart(arg, "SagaInitiate");
                        break;
                    case "MassTransit.Saga.Initiate.Stop":
                        OnStop("SagaInitiate");
                        break;
                    case "MassTransit.Saga.Orchestrate.Start":
                        OnConsumeStart(arg, "SagaOrchestrate");
                        break;
                    case "MassTransit.Saga.Orchestrate.Stop":
                        OnStop("SagaOrchestrate");
                        break;
                    case "MassTransit.Saga.Observe.Start":
                        OnConsumeStart(arg, "SagaObserve");
                        break;
                    case "MassTransit.Saga.Observe.Stop":
                        OnStop("SagaObserve");
                        break;

                    // Routing Slip (Courier) Activity events
                    case "MassTransit.Activity.Execute.Start":
                        OnConsumeStart(arg, "ActivityExecute");
                        break;
                    case "MassTransit.Activity.Execute.Stop":
                        OnStop("ActivityExecute");
                        break;
                    case "MassTransit.Activity.Compensate.Start":
                        OnConsumeStart(arg, "ActivityCompensate");
                        break;
                    case "MassTransit.Activity.Compensate.Stop":
                        OnStop("ActivityCompensate");
                        break;

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

        /// <summary>
        /// Extracts the trace ID from an Activity. Handles both W3C format (00-{traceId}-{spanId}-{flags})
        /// and hierarchical format (uses RootId).
        /// </summary>
        private static string? ExtractTraceId(System.Diagnostics.Activity? activity)
        {
            if (activity == null)
            {
                return null;
            }

            var activityId = activity.Id;
            if (string.IsNullOrEmpty(activityId))
            {
                return null;
            }

            // W3C format: 00-{traceId}-{spanId}-{flags}
            // Example: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
            if (activityId.Length >= 55 && activityId[2] == '-')
            {
                return activityId.Substring(3, 32);
            }

            // Hierarchical format: use RootId
            return activity.RootId;
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

        private void OnConsumeStart(object? arg, string operationType)
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
                "MassTransitDiagnosticObserver.OnConsumeStart: Destination={Destination}, MessageType={MessageType}, OperationType={OperationType}",
                destinationAddress,
                messageType,
                operationType);

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
                StoreScope(operationType, activityId, scope);

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
            var activity = System.Diagnostics.Activity.Current;
            var activityId = activity?.Id;
            var traceId = ExtractTraceId(activity);

            if (string.IsNullOrEmpty(activityId))
            {
                Log.Debug("MassTransitDiagnosticObserver.OnStop: No activity ID for {OperationType}", operationType);
                return;
            }

            var key = $"{operationType}:{activityId}";
            if (_activeScopes.TryRemove(key, out var scope))
            {
                // Check for exceptions captured by NotifyFaultedIntegration (CallTarget)
                // MassTransit 7 does not expose exceptions through DiagnosticSource events,
                // so we use bytecode instrumentation to capture them from NotifyFaulted calls.
                // We use TraceId to look up exceptions because NotifyFaulted may be called
                // from a child activity (Handle/Saga) while this Stop event fires on the
                // parent activity (Consume).
                if (!string.IsNullOrEmpty(traceId))
                {
                    var exception = MassTransitExceptionStore.TryGetAndRemoveException(traceId!);
                    if (exception != null)
                    {
                        MassTransitCommon.SetException(scope, exception);
                        Log.Debug(
                            "MassTransitDiagnosticObserver.OnStop: Set exception for key '{Key}' (TraceId={TraceId}): {ExceptionType}",
                            key,
                            traceId,
                            exception.GetType().Name);
                    }
                }

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

        private void StoreScope(string operationType, string activityId, Scope scope)
        {
            var key = $"{operationType}:{activityId}";
            _activeScopes[key] = scope;
            Log.Debug("MassTransitDiagnosticObserver.StoreScope: Stored scope with key '{Key}'", key);
        }
    }
}
#endif
