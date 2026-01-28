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
using Datadog.Trace.Propagators;

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
            Log.Information("MassTransitDiagnosticObserver.IsSubscriberEnabled: {Enabled}", enabled);
            return enabled;
        }

        public override IDisposable? SubscribeIfMatch(System.Diagnostics.DiagnosticListener diagnosticListener)
        {
            Log.Information("MassTransitDiagnosticObserver.SubscribeIfMatch: Checking listener '{ListenerName}'", diagnosticListener.Name);

            if (diagnosticListener.Name == ListenerName)
            {
                // Subscribe without predicate to receive all events
                var subscription = diagnosticListener.Subscribe(this);
                Log.Information("MassTransitDiagnosticObserver: Subscribed to '{ListenerName}'", diagnosticListener.Name);
                return subscription;
            }

            return null;
        }

        protected override void OnNext(string eventName, object arg)
        {
            Log.Information(
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

        private static T? TryGetProperty<T>(object? obj, string propertyName)
        {
            if (obj == null)
            {
                return default;
            }

            try
            {
                var property = obj.GetType().GetProperty(propertyName);
                if (property != null)
                {
                    var value = property.GetValue(obj);
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitDiagnosticObserver.TryGetProperty: Failed to get property '{PropertyName}'", propertyName);
            }

            return default;
        }

        private static string? GetMessageType(object? context)
        {
            if (context == null)
            {
                return null;
            }

            try
            {
                // Try generic type argument - MassTransit contexts are typically generic
                var contextType = context.GetType();
                if (contextType.IsGenericType)
                {
                    var genericArgs = contextType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        return genericArgs[0].Name;
                    }
                }

                // Try SupportedMessageTypes property
                var supportedTypes = TryGetProperty<string[]>(context, "SupportedMessageTypes");
                if (supportedTypes != null && supportedTypes.Length > 0)
                {
                    return string.Join(",", supportedTypes);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitDiagnosticObserver.GetMessageType: Failed to get message type");
            }

            return null;
        }

        private static void InjectTraceContext(object? sendContext, Scope scope)
        {
            if (sendContext == null || scope.Span == null)
            {
                return;
            }

            try
            {
                // Get Headers property from SendContext
                var headers = TryGetProperty<object>(sendContext, "Headers");
                if (headers == null)
                {
                    Log.Debug("MassTransitDiagnosticObserver.InjectTraceContext: No Headers property found");
                    return;
                }

                // Use SendContextHeadersAdapter to inject trace context
                var headersAdapter = new SendContextHeadersAdapter(headers);
                var context = new PropagationContext(scope.Span.Context, Baggage.Current);
                Tracer.Instance.TracerManager.SpanContextPropagator.Inject(context, headersAdapter);

                Log.Debug(
                    "MassTransitDiagnosticObserver.InjectTraceContext: Injected trace context TraceId={TraceId}",
                    scope.Span.TraceId);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitDiagnosticObserver.InjectTraceContext: Failed to inject trace context");
            }
        }

        private static PropagationContext ExtractTraceContext(object? receiveContext)
        {
            if (receiveContext == null)
            {
                return default;
            }

            try
            {
                // Try TransportHeaders first (ReceiveContext)
                var headers = TryGetProperty<object>(receiveContext, "TransportHeaders");

                // If not found, try Headers (ConsumeContext)
                if (headers == null)
                {
                    headers = TryGetProperty<object>(receiveContext, "Headers");
                }

                if (headers == null)
                {
                    Log.Debug("MassTransitDiagnosticObserver.ExtractTraceContext: No headers found");
                    return default;
                }

                // Use ContextPropagation to extract trace context from headers
                var headersAdapter = new ContextPropagation(headers);
                var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headersAdapter);

                if (extractedContext.SpanContext != null)
                {
                    Log.Debug(
                        "MassTransitDiagnosticObserver.ExtractTraceContext: Extracted TraceId={TraceId}, SpanId={SpanId}",
                        extractedContext.SpanContext.TraceId,
                        extractedContext.SpanContext.SpanId);
                }
                else
                {
                    Log.Debug("MassTransitDiagnosticObserver.ExtractTraceContext: No trace context found in headers");
                }

                return extractedContext;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitDiagnosticObserver.ExtractTraceContext: Failed to extract trace context");
                return default;
            }
        }

        private void OnSendStart(object? arg)
        {
            Log.Information("MassTransitDiagnosticObserver.OnSendStart: Starting");

            if (arg == null)
            {
                Log.Warning("MassTransitDiagnosticObserver.OnSendStart: arg is null");
                return;
            }

            var activityId = GetCurrentActivityId();
            Log.Information("MassTransitDiagnosticObserver.OnSendStart: ActivityId={ActivityId}", activityId);

            // Extract destination and message type from the SendContext
            var destinationAddress = TryGetProperty<Uri>(arg, "DestinationAddress")?.ToString();
            var messageType = GetMessageType(arg);

            Log.Information(
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
                var messageId = TryGetProperty<Guid?>(arg, "MessageId");
                var conversationId = TryGetProperty<Guid?>(arg, "ConversationId");
                var correlationId = TryGetProperty<Guid?>(arg, "CorrelationId");
                MassTransitCommon.SetContextTags(scope, messageId, conversationId, correlationId);

                // Inject trace context into message headers for distributed tracing
                InjectTraceContext(arg, scope);

                Log.Information(
                    "MassTransitDiagnosticObserver.OnSendStart: Created span TraceId={TraceId}, SpanId={SpanId}",
                    scope.Span?.TraceId,
                    scope.Span?.SpanId);
            }
            else
            {
                Log.Warning(
                    "MassTransitDiagnosticObserver.OnSendStart: No scope created or no activityId. Scope={ScopeNotNull}, ActivityId={ActivityId}",
                    scope != null,
                    activityId);
            }
        }

        private void OnReceiveStart(object? arg)
        {
            Log.Information("MassTransitDiagnosticObserver.OnReceiveStart: Starting");

            if (arg == null)
            {
                Log.Warning("MassTransitDiagnosticObserver.OnReceiveStart: arg is null");
                return;
            }

            var activityId = GetCurrentActivityId();
            Log.Information("MassTransitDiagnosticObserver.OnReceiveStart: ActivityId={ActivityId}", activityId);

            // Extract input address from ReceiveContext
            var inputAddress = TryGetProperty<Uri>(arg, "InputAddress")?.ToString();
            var messageType = GetMessageType(arg);

            Log.Information(
                "MassTransitDiagnosticObserver.OnReceiveStart: InputAddress={InputAddress}, MessageType={MessageType}",
                inputAddress,
                messageType);

            // Extract parent context from headers for distributed tracing
            var parentContext = ExtractTraceContext(arg);

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
                var messageId = TryGetProperty<Guid?>(arg, "MessageId");
                var conversationId = TryGetProperty<Guid?>(arg, "ConversationId");
                var correlationId = TryGetProperty<Guid?>(arg, "CorrelationId");
                MassTransitCommon.SetContextTags(scope, messageId, conversationId, correlationId);

                Log.Information(
                    "MassTransitDiagnosticObserver.OnReceiveStart: Created span TraceId={TraceId}, SpanId={SpanId}, ParentId={ParentId}",
                    scope.Span?.TraceId,
                    scope.Span?.SpanId,
                    parentContext.SpanContext?.SpanId);
            }
        }

        private void OnConsumeStart(object? arg)
        {
            Log.Information("MassTransitDiagnosticObserver.OnConsumeStart: Starting");

            if (arg == null)
            {
                Log.Warning("MassTransitDiagnosticObserver.OnConsumeStart: arg is null");
                return;
            }

            var activityId = GetCurrentActivityId();
            Log.Information("MassTransitDiagnosticObserver.OnConsumeStart: ActivityId={ActivityId}", activityId);

            // For consume, we get a ConsumeContext - try multiple address properties
            // Try DestinationAddress first (where the message was sent to)
            var destinationAddress = TryGetProperty<Uri>(arg, "DestinationAddress")?.ToString();

            // If not available, try ReceiveContext.InputAddress (where the message was received)
            if (string.IsNullOrEmpty(destinationAddress))
            {
                var receiveContext = TryGetProperty<object>(arg, "ReceiveContext");
                destinationAddress = TryGetProperty<Uri>(receiveContext, "InputAddress")?.ToString();
            }

            // If still not available, try SourceAddress (where the message came from)
            if (string.IsNullOrEmpty(destinationAddress))
            {
                destinationAddress = TryGetProperty<Uri>(arg, "SourceAddress")?.ToString();
            }

            var messageType = GetMessageType(arg);

            Log.Information(
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
                var messageId = TryGetProperty<Guid?>(arg, "MessageId");
                var conversationId = TryGetProperty<Guid?>(arg, "ConversationId");
                var correlationId = TryGetProperty<Guid?>(arg, "CorrelationId");
                MassTransitCommon.SetContextTags(scope, messageId, conversationId, correlationId);

                Log.Information(
                    "MassTransitDiagnosticObserver.OnConsumeStart: Created span TraceId={TraceId}, SpanId={SpanId}",
                    scope.Span?.TraceId,
                    scope.Span?.SpanId);
            }
        }

        private void OnStop(string operationType)
        {
            var activityId = GetCurrentActivityId();
            Log.Information(
                "MassTransitDiagnosticObserver.OnStop: OperationType={OperationType}, ActivityId={ActivityId}",
                operationType,
                activityId);

            if (string.IsNullOrEmpty(activityId))
            {
                Log.Warning("MassTransitDiagnosticObserver.OnStop: No activity ID for {OperationType}", operationType);
                return;
            }

            var key = $"{operationType}:{activityId}";
            if (_activeScopes.TryRemove(key, out var scope))
            {
                MassTransitCommon.CloseScope(scope, operationType);
                Log.Information("MassTransitDiagnosticObserver.OnStop: Closed scope for key '{Key}'", key);
            }
            else
            {
                Log.Warning(
                    "MassTransitDiagnosticObserver.OnStop: No scope found for key '{Key}'. Active keys: [{Keys}]",
                    key,
                    string.Join(", ", _activeScopes.Keys));
            }
        }

        private void OnException(string operationType, object? arg)
        {
            var activityId = GetCurrentActivityId();
            Log.Information(
                "MassTransitDiagnosticObserver.OnException: OperationType={OperationType}, ActivityId={ActivityId}",
                operationType,
                activityId);

            if (string.IsNullOrEmpty(activityId))
            {
                return;
            }

            var key = $"{operationType}:{activityId}";
            if (_activeScopes.TryGetValue(key, out var scope))
            {
                var exception = arg as Exception ?? TryGetProperty<Exception>(arg, "Exception");
                MassTransitCommon.SetException(scope, exception);
                Log.Information("MassTransitDiagnosticObserver.OnException: Set error on scope for key '{Key}'", key);
            }
        }

        private void StoreScope(string operationType, string activityId, Scope scope)
        {
            var key = $"{operationType}:{activityId}";
            _activeScopes[key] = scope;
            Log.Information("MassTransitDiagnosticObserver.StoreScope: Stored scope with key '{Key}'", key);
        }
    }
}
#endif
