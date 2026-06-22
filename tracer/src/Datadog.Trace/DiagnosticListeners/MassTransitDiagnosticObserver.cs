// <copyright file="MassTransitDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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
    /// - Receive events: Extract parent context from TransportHeaders (message attributes/metadata)
    /// - Consume/Handle events: Extract parent context from Headers (message envelope headers)
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
            return Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.MassTransit);
        }

        protected override void OnNext(string eventName, object arg)
        {
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

            // Extract metadata from SendContext — returns the duck-typed proxy for reuse
            var sendContextProxy = MassTransitCommon.ExtractSendContextMetadata(arg, out var destinationAddress, out var messageId, out var conversationId, out var correlationId);
            var scope = MassTransitCommon.CreateProduceSpan(Tracer.Instance, destinationAddress, messageType: null);

            if (scope is not null)
            {
                MassTransitCommon.SetContextTags(scope, messageId, conversationId, correlationId);

                // Inject trace context into message headers — reuses the proxy from above, no second DuckCast
                MassTransitCommon.InjectTraceContext(Tracer.Instance, sendContextProxy, scope);
            }
        }

        private void OnReceiveStart(object? arg)
        {
            if (arg == null)
            {
                return;
            }

            // Duck cast to IReceiveContext — BaseReceiveContext.InputAddress and TransportHeaders
            // are public concrete properties, so this always succeeds for well-formed receive contexts.
            var castSucceeded = arg.TryDuckCast<IReceiveContext>(out var receiveCtx);
            var inputAddress = receiveCtx?.InputAddress?.ToString();
            var transportHeaders = receiveCtx?.TransportHeaders;

            PropagationContext parentContext = default;
            if (transportHeaders != null)
            {
                var adapter = new ContextPropagationExtractAdapter(transportHeaders);
                parentContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(adapter);
            }
            else if (!castSucceeded)
            {
                Log.Warning(
                    "MassTransitDiagnosticObserver.OnReceiveStart: Duck cast failed for ArgType={ArgType} — receive span will have no parent",
                    arg.GetType().FullName);
            }

            // Merge extracted baggage into ambient context. CreateReceiveSpan only forwards
            // parentContext.SpanContext to StartActiveInternal, so without this baggage from the
            // incoming message would be dropped instead of flowing to the consume span.
            parentContext = parentContext.MergeBaggageInto(Baggage.Current);

            MassTransitCommon.CreateReceiveSpan(Tracer.Instance, inputAddress, parentContext);
        }

        private void OnConsumeStart(object? arg, string operationType)
        {
            if (arg == null)
            {
                return;
            }

            var directCastSucceeded = arg.TryDuckCast<IConsumeContext>(out var consumeContext);
            if (!directCastSucceeded)
            {
                var innerCastSucceeded = arg.TryDuckCast<IMessageConsumeContextInner>(out var inner);
                if (innerCastSucceeded && inner?.Context != null)
                {
                    inner.Context.TryDuckCast<IConsumeContext>(out consumeContext);
                }
                else
                {
                    Log.Warning(
                        "MassTransitDiagnosticObserver.OnConsumeStart: All casts failed ArgType={ArgType}, DirectCast=false, InnerCast={InnerCast}, InnerContext={InnerContext}",
                        arg.GetType().FullName,
                        innerCastSucceeded,
                        inner?.Context?.GetType().FullName ?? "null");
                }
            }

            var messageType = MassTransitCommon.GetConsumeMessageType(consumeContext);

            PropagationContext parentContext = default;
            if (consumeContext?.Headers != null)
            {
                var adapter = new ContextPropagationExtractAdapter(consumeContext.Headers);
                parentContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(adapter);
            }

            // Merge extracted baggage into ambient context. CreateProcessSpan only forwards
            // parentContext.SpanContext to StartActiveInternal, and the parent override below
            // also rebuilds the context from Baggage.Current, so without this the baggage from
            // the incoming message would be dropped.
            parentContext = parentContext.MergeBaggageInto(Baggage.Current);

            var inputAddress = consumeContext?.ReceiveContext?.InputAddress?.ToString();

            // For Process/Consume/Handle spans, check if there's an active Receive span to use as parent.
            var activeScope = Tracer.Instance.ActiveScope;
            if (activeScope?.Span != null &&
                activeScope.Span.OperationName == MassTransitConstants.ReceiveOperationName &&
                activeScope.Span.GetTag(Tags.InstrumentationName) == MassTransitConstants.ComponentTagName)
            {
                parentContext = new PropagationContext(activeScope.Span.Context as SpanContext, Baggage.Current);
            }

            MassTransitCommon.CreateProcessSpan(Tracer.Instance, inputAddress, messageType, parentContext);
        }

        private void OnStop(string operationType)
        {
            // Datadog scopes use AsyncLocal, so ActiveScope at Stop time is exactly the scope
            // created at Start time for this operation (MassTransit fires Stop events in LIFO order).
            var scope = Tracer.Instance.ActiveScope as Scope;

            if (scope == null)
            {
                return;
            }

            // Guard: verify this is a MassTransit span before closing, to avoid silently closing
            // an unrelated span if MT event ordering is unexpected.
            if (scope.Span.GetTag(Tags.InstrumentationName) != MassTransitConstants.ComponentTagName)
            {
                Log.Warning(
                    "MassTransitDiagnosticObserver.OnStop: Active scope is not a MassTransit span " +
                    "(component={Component}, operation={Operation}) — skipping close for {OperationType}",
                    scope.Span.GetTag(Tags.InstrumentationName),
                    scope.Span.OperationName,
                    operationType);
                return;
            }

            scope.Close();
        }
    }
}
