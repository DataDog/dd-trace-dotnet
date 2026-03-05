// <copyright file="MassTransitDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

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
    /// MassTransit 7 emits the following diagnostic events:
    /// - MassTransit.Transport.Send (Start/Stop) - When messages are sent → Creates producer spans
    /// - MassTransit.Transport.Receive (Start/Stop) - When messages are received (NOT instrumented)
    /// - MassTransit.Consumer.Consume (Start/Stop) - When a consumer processes a message → Creates consumer spans
    /// - MassTransit.Consumer.Handle (Start/Stop) - When a handler processes a message → Creates consumer spans
    /// - MassTransit.Saga.* (Start/Stop) - When saga state machines process events → Creates consumer spans
    /// - MassTransit.Activity.* (Start/Stop) - When Routing Slip activities execute/compensate → Creates consumer spans
    /// <para/>
    /// Context propagation:
    /// - Send events: Inject trace context into message headers via InjectTraceContext()
    /// - Consume/Handle events: Extract parent context from message headers via ExtractTraceContext()
    /// - This links consumer spans to producer spans across the message bus
    /// <para/>
    /// Scope lifecycle:
    /// Datadog scopes use AsyncLocal, so the active scope at Stop time is exactly the scope created
    /// at Start time for that operation, provided MassTransit fires events in proper order
    /// (which it does: Consume.Stop fires before Receive.Stop). OnStop validates the active scope
    /// is a MassTransit span before closing it to guard against ordering issues.
    /// <para/>
    /// Exception handling:
    /// MassTransit 7 does NOT emit exception information through DiagnosticSource events (Stop event arg
    /// is always null). We use CallTarget instrumentation on BaseReceiveContext.NotifyFaulted to set
    /// error tags directly on the active span — AsyncLocal ensures it is the correct span.
    /// </remarks>
    internal sealed class MassTransitDiagnosticObserver : DiagnosticObserver
    {
        private const string DiagnosticListenerName = "MassTransit";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MassTransitDiagnosticObserver>();

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
            try
            {
                Log.Debug(
                    "MassTransitDiagnosticObserver.OnNext: Event='{EventName}', ArgType={ArgType}",
                    eventName,
                    arg?.GetType().FullName ?? "null");

                switch (eventName)
                {
                    // Send events (producer spans)
                    case "MassTransit.Transport.Send.Start":
                        OnProduceStart(arg);
                        break;
                    case "MassTransit.Transport.Send.Stop":
                        OnStop("Send");
                        break;

                    // Receive events (transport level - parent of Consume/Handle)
                    // For Receive events, arg IS the ReceiveContext directly (not ConsumeContext)
                    case "MassTransit.Transport.Receive.Start":
                        OnReceiveStart(arg);
                        break;
                    case "MassTransit.Transport.Receive.Stop":
                        OnStop("Receive");
                        break;

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
                    // NOTE: MassTransit fires MULTIPLE events for the same saga operation:
                    // - Saga.Send (when saga receives message) + Saga.RaiseEvent (when state machine transitions)
                    // We only instrument RaiseEvent to avoid duplicate spans
                    // Saga.Send.Start/Stop - SKIPPED to avoid duplicates

                    // Saga.RaiseEvent fires when a saga state machine raises an event
                    case "MassTransit.Saga.RaiseEvent.Start":
                        OnConsumeStart(arg, "SagaRaiseEvent");
                        break;
                    case "MassTransit.Saga.RaiseEvent.Stop":
                        OnStop("SagaRaiseEvent");
                        break;

                    // Additional Saga events - SKIPPED to avoid duplicates with RaiseEvent
                    // These fire alongside RaiseEvent for specific scenarios
                    // SendQuery, Initiate, Orchestrate, Observe.Start/Stop - SKIPPED

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

        private void OnProduceStart(object? arg)
        {
            if (arg == null)
            {
                return;
            }

            // Extract metadata from SendContext using duck typing
            MassTransitCommon.ExtractSendContextMetadata(arg, out var destinationAddress, out var messageId, out var conversationId, out var correlationId);
            var messageType = MassTransitCommon.GetMessageType(arg);

            Log.Debug(
                "MassTransitDiagnosticObserver.OnProduceStart: Destination={Destination}, MessageType={MessageType}",
                destinationAddress,
                messageType);

            var scope = MassTransitCommon.CreateProduceSpan(Tracer.Instance, destinationAddress, messageType);

            if (scope is not null)
            {
                // Set additional context tags
                MassTransitCommon.SetContextTags(scope, messageId, conversationId, correlationId);

                // Inject trace context into message headers for distributed tracing
                MassTransitCommon.InjectTraceContext(Tracer.Instance, arg, scope);

                Log.Debug(
                    "MassTransitDiagnosticObserver.OnProduceStart: Created span TraceId={TraceId}, SpanId={SpanId}",
                    scope.Span.TraceId,
                    scope.Span.SpanId);
            }
        }

        private void OnReceiveStart(object? arg)
        {
            if (arg == null)
            {
                Log.Debug("MassTransitDiagnosticObserver.OnReceiveStart: arg is null");
                return;
            }

            Log.Debug(
                "MassTransitDiagnosticObserver.OnReceiveStart: Processing ReceiveContext, ArgType={ArgType}",
                arg.GetType().FullName);

            // For Receive events, arg IS the ReceiveContext directly (e.g., InMemoryReceiveContext, RabbitMqReceiveContext)
            // Extract InputAddress and TransportHeaders from the ReceiveContext
            var inputAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "InputAddress")?.ToString();
            var transportHeaders = MassTransitCommon.TryGetProperty<object>(arg, "TransportHeaders");

            Log.Debug(
                "MassTransitDiagnosticObserver.OnReceiveStart: InputAddress={InputAddress}, HasTransportHeaders={HasHeaders}",
                inputAddress ?? "null",
                transportHeaders != null);

            // Extract parent context from TransportHeaders for distributed tracing
            // For Receive events, we need to extract from TransportHeaders, not the context directly
            PropagationContext parentContext = default;
            if (transportHeaders != null)
            {
                // Create a wrapper object that has a Headers property pointing to TransportHeaders
                // so ExtractTraceContext can use it
                var headersWrapper = new { Headers = transportHeaders };
                parentContext = MassTransitCommon.ExtractTraceContext(Tracer.Instance, headersWrapper);

                Log.Debug(
                    "MassTransitDiagnosticObserver.OnReceiveStart: ExtractedParentContext, HasSpanContext={HasContext}",
                    parentContext.SpanContext != null);
            }

            var scope = MassTransitCommon.CreateReceiveSpan(Tracer.Instance, inputAddress, parentContext);

            if (scope != null)
            {
                Log.Debug(
                    "MassTransitDiagnosticObserver.OnReceiveStart: Created span TraceId={TraceId}, SpanId={SpanId}, ParentId={ParentId}",
                    scope.Span.TraceId,
                    scope.Span.SpanId,
                    scope.Span.Context.ParentId);
            }
            else
            {
                Log.Debug("MassTransitDiagnosticObserver.OnReceiveStart: Scope not created");
            }
        }

        private void OnConsumeStart(object? arg, string operationType)
        {
            if (arg == null)
            {
                return;
            }

            // For consume, we get a ConsumeContext
            // MT8 OTEL uses InputAddress (the queue name) for consumer spans, not DestinationAddress
            // The InputAddress gives clean queue names like "GettingStarted", "OrderState"
            // while DestinationAddress might be a URN like "loopback://localhost/urn:message:..."

            // Note: We use reflection here because the most common context type (MessageConsumeContext<T>)
            // uses explicit interface implementation for all properties, which duck typing cannot handle.
            // Duck typing only works for some proxy types like CorrelationIdConsumeContextProxy<T>.
            string? inputAddress = null;
            var receiveContext = MassTransitCommon.TryGetProperty<object>(arg, "ReceiveContext");
            if (receiveContext != null)
            {
                inputAddress = MassTransitCommon.TryGetProperty<Uri>(receiveContext, "InputAddress")?.ToString();
            }

            // Fallback to DestinationAddress if InputAddress not available
            if (string.IsNullOrEmpty(inputAddress))
            {
                inputAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "DestinationAddress")?.ToString();
            }

            // If still not available, try SourceAddress
            if (string.IsNullOrEmpty(inputAddress))
            {
                inputAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "SourceAddress")?.ToString();
            }

            var messageType = MassTransitCommon.GetMessageType(arg);

            Log.Debug(
                "MassTransitDiagnosticObserver.OnConsumeStart: InputAddress={InputAddress}, MessageType={MessageType}, OperationType={OperationType}",
                inputAddress,
                messageType,
                operationType);

            // Extract parent context from headers for distributed tracing
            var parentContext = MassTransitCommon.ExtractTraceContext(Tracer.Instance, arg);

            // For Process/Consume/Handle spans, check if there's an active Receive span to use as parent
            // If a Receive span is active, use it instead of the extracted context from headers
            var activeScope = Tracer.Instance.ActiveScope;
            if (activeScope?.Span != null &&
                activeScope.Span.OperationName == "masstransit.receive" &&
                activeScope.Span.GetTag("component") == MassTransitConstants.ComponentTagName)
            {
                // Use the active Receive span as the parent for Process/Consume spans
                parentContext = new PropagationContext(activeScope.Span.Context as SpanContext, Baggage.Current);

                Log.Debug(
                    "MassTransitDiagnosticObserver.OnConsumeStart: Using active Receive span as parent, ParentSpanId={ParentSpanId}",
                    activeScope.Span.SpanId);
            }

            var scope = MassTransitCommon.CreateProcessSpan(Tracer.Instance, inputAddress, messageType, parentContext);

            if (scope != null)
            {
                // Note: MT8 OTEL instrumentation does not set messageId/conversationId/correlationId tags
                // on consumer "process" spans, only on "receive" spans. We match that behavior.

                Log.Debug(
                    "MassTransitDiagnosticObserver.OnConsumeStart: Created span TraceId={TraceId}, SpanId={SpanId}",
                    scope.Span.TraceId,
                    scope.Span.SpanId);
            }
        }

        private void OnStop(string operationType)
        {
            // Datadog scopes use AsyncLocal, so ActiveScope at Stop time is exactly the scope
            // created at Start time for this operation (MassTransit fires Stop events in LIFO order).
            var scope = Tracer.Instance.ActiveScope as Scope;

            if (scope == null)
            {
                Log.Debug("MassTransitDiagnosticObserver.OnStop: No active scope for {OperationType}", operationType);
                return;
            }

            // Guard: verify this is a MassTransit span before closing, to avoid silently closing
            // an unrelated span if MT event ordering is unexpected.
            if (scope.Span.GetTag("component") != MassTransitConstants.ComponentTagName)
            {
                Log.Warning(
                    "MassTransitDiagnosticObserver.OnStop: Active scope is not a MassTransit span " +
                    "(component={Component}, operation={Operation}) — skipping close for {OperationType}",
                    scope.Span.GetTag("component"),
                    scope.Span.OperationName,
                    operationType);
                return;
            }

            MassTransitCommon.CloseScope(scope, operationType);
            Log.Debug("MassTransitDiagnosticObserver.OnStop: Closed scope for {OperationType}", operationType);
        }
    }
}
#endif
