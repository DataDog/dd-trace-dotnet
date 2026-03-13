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
    /// - MassTransit.Transport.Receive (Start/Stop) - When messages are received → Creates receive spans
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
                        OnSendStart(arg);
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

        private void OnSendStart(object? arg)
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

                // Set AsyncLocal so the InMemoryTransportMessage constructor hook can copy
                // trace headers into the transport message. The constructor fires synchronously
                // within the same async context, so AsyncLocal is safe here.
                if (scope.Span.Context is SpanContext spanContext)
                {
                    MassTransitCommon.PendingInMemorySpanContext.Value = spanContext;
                }

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

            // Duck cast arg to IReceiveContext — BaseReceiveContext.TransportHeaders is public and
            // returns Headers (JsonTransportHeaders). IReceiveContext.TransportHeaders is duck typed
            // to IHeaders which exposes GetAll() returning KeyValuePair<string, object> items.
            var receiveCtx = arg.DuckCast<IReceiveContext>();
            var inputAddress = receiveCtx?.InputAddress?.ToString();
            var transportHeaders = receiveCtx?.TransportHeaders;

            Log.Debug(
                "MassTransitDiagnosticObserver.OnReceiveStart: InputAddress={InputAddress}, HasTransportHeaders={HasHeaders}",
                inputAddress ?? "null",
                transportHeaders != null);

            // Pass transportHeaders (already IHeaders proxy) directly to the extract adapter.
            // ContextPropagationExtractAdapter.GetValues() iterates GetAll() and casts each item
            // as KeyValuePair<string, object> — no duck typing of items needed.
            PropagationContext parentContext;
            if (transportHeaders != null)
            {
                var adapter = new ContextPropagationExtractAdapter(transportHeaders);
                parentContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(adapter);
            }
            else
            {
                parentContext = MassTransitCommon.ExtractTraceContext(Tracer.Instance, arg);
            }

            Log.Debug(
                "MassTransitDiagnosticObserver.OnReceiveStart: ExtractedParentContext, HasSpanContext={HasContext}",
                parentContext.SpanContext != null);

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

            var messageType = MassTransitCommon.GetMessageType(arg);

            Log.Debug(
                "MassTransitDiagnosticObserver.OnConsumeStart: MessageType={MessageType}, OperationType={OperationType}",
                messageType,
                operationType);

            // Try duck casting to IConsumeContext to get Headers.
            // Fails for MessageConsumeContext<T> (the most common type) because it implements
            // Headers as an explicit interface: `Headers MessageContext.Headers => _context.Headers`
            // Duck typing only finds public class members, not explicit interface implementations.
            // In that case TryDuckCast returns false and we fall back to reflection-based ExtractTraceContext.
            PropagationContext parentContext;
            if (arg.TryDuckCast<IConsumeContext>(out var consumeCtx) && consumeCtx.Headers != null)
            {
                var adapter = new ContextPropagationExtractAdapter(consumeCtx.Headers);
                parentContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(adapter);
            }
            else
            {
                parentContext = MassTransitCommon.ExtractTraceContext(Tracer.Instance, arg);
            }

            // Get InputAddress from ReceiveContext — via duck typing if available, reflection otherwise.
            var inputAddress = consumeCtx?.ReceiveContext?.InputAddress?.ToString();
            if (string.IsNullOrEmpty(inputAddress))
            {
                var rc = MassTransitCommon.TryGetProperty<object>(arg, "ReceiveContext");
                inputAddress = rc != null ? MassTransitCommon.TryGetProperty<Uri>(rc, "InputAddress")?.ToString() : null;
            }

            // For Process/Consume/Handle spans, check if there's an active Receive span to use as parent.
            var activeScope = Tracer.Instance.ActiveScope;
            if (activeScope?.Span != null &&
                activeScope.Span.OperationName == "masstransit.receive" &&
                activeScope.Span.GetTag("component") == MassTransitConstants.ComponentTagName)
            {
                parentContext = new PropagationContext(activeScope.Span.Context as SpanContext, Baggage.Current);

                Log.Debug(
                    "MassTransitDiagnosticObserver.OnConsumeStart: Using active Receive span as parent, ParentSpanId={ParentSpanId}",
                    activeScope.Span.SpanId);
            }

            var scope = MassTransitCommon.CreateProcessSpan(Tracer.Instance, inputAddress, messageType, parentContext);

            if (scope != null)
            {
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
