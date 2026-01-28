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
    /// MassTransit 7 emits DiagnosticSource events but the Activities it creates are minimal.
    /// We create our own Datadog spans based on the diagnostic events.
    /// </summary>
    /// <remarks>
    /// MassTransit emits the following diagnostic events:
    /// - MassTransit.Transport.Send (Start/Stop) - When messages are sent
    /// - MassTransit.Transport.Receive (Start/Stop) - When messages are received
    /// - MassTransit.Consumer.Consume (Start/Stop) - When a consumer processes a message
    /// - MassTransit.Consumer.Handle (Start/Stop) - When a handler processes a message
    /// - MassTransit.Saga.* - Various saga events
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
                // Subscribe without predicate to receive all events
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

                    // Receive events (consumer spans)
                    case "MassTransit.Transport.Receive.Start":
                        OnReceiveStart(arg);
                        break;
                    case "MassTransit.Transport.Receive.Stop":
                        OnStop("Receive");
                        break;
                    case "MassTransit.Transport.Receive.Exception":
                        OnException("Receive", arg);
                        break;

                    // Consumer Consume events (process spans)
                    case "MassTransit.Consumer.Consume.Start":
                        OnConsumeStart(arg);
                        break;
                    case "MassTransit.Consumer.Consume.Stop":
                        OnStop("Consume");
                        break;
                    case "MassTransit.Consumer.Consume.Exception":
                        OnException("Consume", arg);
                        break;

                    // Consumer Handle events
                    case "MassTransit.Consumer.Handle.Start":
                        OnConsumeStart(arg); // Treat same as Consume
                        break;
                    case "MassTransit.Consumer.Handle.Stop":
                        OnStop("Consume"); // Use same key as Consume
                        break;
                    case "MassTransit.Consumer.Handle.Exception":
                        OnException("Consume", arg);
                        break;

                    default:
                        // Log but don't process unknown events (saga events, etc.)
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
            var id = activity?.Id ?? string.Empty;
            Log.Debug(
                "MassTransitDiagnosticObserver.GetCurrentActivityId: Activity.Current={CurrentNotNull}, Id={Id}",
                activity != null,
                id);
            return id;
        }

        private void OnSendStart(object? arg)
        {
            Log.Debug("MassTransitDiagnosticObserver.OnSendStart: Starting");

            if (arg == null)
            {
                Log.Debug("MassTransitDiagnosticObserver.OnSendStart: arg is null");
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
            else
            {
                Log.Debug(
                    "MassTransitDiagnosticObserver.OnSendStart: No scope created or no activityId. Scope={ScopeNotNull}, ActivityId={ActivityId}",
                    scope != null,
                    activityId);
            }
        }

        private void OnReceiveStart(object? arg)
        {
            Log.Debug("MassTransitDiagnosticObserver.OnReceiveStart: Starting");

            if (arg == null)
            {
                Log.Debug("MassTransitDiagnosticObserver.OnReceiveStart: arg is null");
                return;
            }

            var activityId = GetCurrentActivityId();

            // Extract input address from ReceiveContext
            var inputAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "InputAddress")?.ToString();
            var messageType = MassTransitCommon.GetMessageType(arg);

            Log.Debug(
                "MassTransitDiagnosticObserver.OnReceiveStart: InputAddress={InputAddress}, MessageType={MessageType}",
                inputAddress,
                messageType);

            // Extract parent context from headers for distributed tracing
            var parentContext = MassTransitCommon.ExtractTraceContext(Tracer.Instance, arg);

            var scope = MassTransitCommon.CreateConsumerScope(
                Tracer.Instance,
                MassTransitConstants.OperationReceive,
                inputAddress,
                messageType,
                parentContext);

            if (scope != null && !string.IsNullOrEmpty(activityId))
            {
                StoreScope("Receive", activityId, scope);

                // Set additional context tags
                var messageId = MassTransitCommon.TryGetProperty<Guid?>(arg, "MessageId");
                var conversationId = MassTransitCommon.TryGetProperty<Guid?>(arg, "ConversationId");
                var correlationId = MassTransitCommon.TryGetProperty<Guid?>(arg, "CorrelationId");
                MassTransitCommon.SetContextTags(scope, messageId, conversationId, correlationId);

                Log.Debug(
                    "MassTransitDiagnosticObserver.OnReceiveStart: Created span TraceId={TraceId}, SpanId={SpanId}, ParentId={ParentId}",
                    scope.Span?.TraceId,
                    scope.Span?.SpanId,
                    parentContext.SpanContext?.SpanId);
            }
        }

        private void OnConsumeStart(object? arg)
        {
            Log.Debug("MassTransitDiagnosticObserver.OnConsumeStart: Starting");

            if (arg == null)
            {
                Log.Debug("MassTransitDiagnosticObserver.OnConsumeStart: arg is null");
                return;
            }

            var activityId = GetCurrentActivityId();

            // For consume, we get a ConsumeContext - try multiple address properties
            // Try DestinationAddress first (where the message was sent to)
            var destinationAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "DestinationAddress")?.ToString();

            // If not available, try ReceiveContext.InputAddress (where the message was received)
            if (string.IsNullOrEmpty(destinationAddress))
            {
                var receiveContext = MassTransitCommon.TryGetProperty<object>(arg, "ReceiveContext");
                destinationAddress = MassTransitCommon.TryGetProperty<Uri>(receiveContext, "InputAddress")?.ToString();
            }

            // If still not available, try SourceAddress (where the message came from)
            if (string.IsNullOrEmpty(destinationAddress))
            {
                destinationAddress = MassTransitCommon.TryGetProperty<Uri>(arg, "SourceAddress")?.ToString();
            }

            var messageType = MassTransitCommon.GetMessageType(arg);

            Log.Debug(
                "MassTransitDiagnosticObserver.OnConsumeStart: Destination={Destination}, MessageType={MessageType}",
                destinationAddress,
                messageType);

            var scope = MassTransitCommon.CreateConsumerScope(
                Tracer.Instance,
                MassTransitConstants.OperationProcess,
                destinationAddress,
                messageType);

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
                    "MassTransitDiagnosticObserver.OnStop: No scope found for key '{Key}'. Active keys: [{Keys}]",
                    key,
                    string.Join(", ", _activeScopes.Keys));
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
