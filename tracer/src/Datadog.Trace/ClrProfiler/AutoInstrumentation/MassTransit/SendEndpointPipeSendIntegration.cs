// <copyright file="SendEndpointPipeSendIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;

/// <summary>
/// MassTransit.Transports.SendEndpoint+SendEndpointPipe`1.Send calltarget instrumentation
/// This instruments the internal pipe's Send method to create send spans and inject trace context headers.
/// This is the common gateway for all MassTransit send operations regardless of transport.
/// MT8 OTEL creates a send span here which forms the parent-child chain: send → receive → process → send
/// </summary>
[InstrumentMethod(
    AssemblyName = MassTransitConstants.MassTransitAssembly,
    TypeName = "MassTransit.Transports.SendEndpoint+SendEndpointPipe`1",
    MethodName = "Send",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["_"],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = MassTransitConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SendEndpointPipeSendIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SendEndpointPipeSendIntegration));

    /// <summary>
    /// OnMethodBegin callback - create send span and inject trace context headers into SendContext
    /// </summary>
    /// <typeparam name="TTarget">Type of the target (SendEndpointPipe)</typeparam>
    /// <typeparam name="TContext">Type of the SendContext</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="context">The send context with headers.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
    {
        Log.Debug("MassTransit SendEndpointPipeSendIntegration.OnMethodBegin() - ENTRY");

        if (context is null)
        {
            Log.Debug("MassTransit SendEndpointPipeSendIntegration - Context is null, skipping");
            return CallTargetState.GetDefault();
        }

        var tracer = Tracer.Instance;
        Scope? scope = null;
        string? destinationAddress = null;
        string? messageType = null;
        string? messagingSystem = "in-memory";
        object? headersObj = null;
        MethodInfo? setMethod = null;

        try
        {
            var contextType = context.GetType();
            Log.Debug("MassTransit SendEndpointPipeSendIntegration - Context type: {ContextType}", contextType.FullName);

            // In MT7, the DestinationAddress may not be set on MessageSendContext yet
            // First try the context, then try the instance (SendEndpointPipe has _endpoint field pointing to SendEndpoint)
            var destAddressProp = contextType.GetProperty("DestinationAddress");
            if (destAddressProp?.GetValue(context) is Uri destUri)
            {
                destinationAddress = destUri.ToString();
                messagingSystem = DetermineMessagingSystem(destinationAddress);
                Log.Debug("MassTransit SendEndpointPipeSendIntegration - Got destination from context: {Destination}", destinationAddress);
            }
            else if (instance != null)
            {
                // Try to get destination from the SendEndpointPipe's parent SendEndpoint
                // In MT7, SendEndpointPipe has a _endpoint field that references the parent SendEndpoint
                var instanceType = instance.GetType();
                Log.Debug("MassTransit SendEndpointPipeSendIntegration - Instance type: {InstanceType}", instanceType.FullName);

                // Try _endpoint field first (MT7 structure: SendEndpointPipe._endpoint -> SendEndpoint.DestinationAddress)
                var endpointField = instanceType.GetField("_endpoint", BindingFlags.NonPublic | BindingFlags.Instance);
                if (endpointField != null)
                {
                    var sendEndpoint = endpointField.GetValue(instance);
                    if (sendEndpoint != null)
                    {
                        var sendEndpointType = sendEndpoint.GetType();
                        Log.Debug("MassTransit SendEndpointPipeSendIntegration - SendEndpoint type: {SendEndpointType}", sendEndpointType.FullName);

                        // The DestinationAddress property might be an explicit interface implementation
                        // Iterate through all properties to find DestinationAddress
                        var allProps = sendEndpointType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var prop in allProps)
                        {
                            if (prop.Name == "DestinationAddress" || prop.Name.EndsWith(".DestinationAddress"))
                            {
                                try
                                {
                                    if (prop.GetValue(sendEndpoint) is Uri propUri)
                                    {
                                        destinationAddress = propUri.ToString();
                                        messagingSystem = DetermineMessagingSystem(destinationAddress);
                                        Log.Debug("MassTransit SendEndpointPipeSendIntegration - Got destination from SendEndpoint property {PropName}: {Destination}", prop.Name, destinationAddress);
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Debug(ex, "MassTransit SendEndpointPipeSendIntegration - Failed to get DestinationAddress from {PropName}", prop.Name);
                                }
                            }
                        }
                    }
                }

                // If _endpoint didn't work, try _context (might be different in some versions)
                if (destinationAddress == null)
                {
                    var contextField = instanceType.GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (contextField != null)
                    {
                        var sendEndpointContext = contextField.GetValue(instance);
                        if (sendEndpointContext != null)
                        {
                            var sendEndpointContextType = sendEndpointContext.GetType();
                            Log.Debug("MassTransit SendEndpointPipeSendIntegration - SendEndpointContext type: {ContextType}", sendEndpointContextType.FullName);

                            // Try EndpointAddress first
                            var endpointAddressProp = sendEndpointContextType.GetProperty("EndpointAddress");
                            if (endpointAddressProp?.GetValue(sendEndpointContext) is Uri endpointUri)
                            {
                                destinationAddress = endpointUri.ToString();
                                messagingSystem = DetermineMessagingSystem(destinationAddress);
                                Log.Debug("MassTransit SendEndpointPipeSendIntegration - Got destination from _context.EndpointAddress: {Destination}", destinationAddress);
                            }
                            else
                            {
                                // Try DestinationAddress
                                var destAddrProp = sendEndpointContextType.GetProperty("DestinationAddress");
                                if (destAddrProp?.GetValue(sendEndpointContext) is Uri destAddr)
                                {
                                    destinationAddress = destAddr.ToString();
                                    messagingSystem = DetermineMessagingSystem(destinationAddress);
                                    Log.Debug("MassTransit SendEndpointPipeSendIntegration - Got destination from _context.DestinationAddress: {Destination}", destinationAddress);
                                }
                            }
                        }
                    }
                }

                // Log all fields for debugging if we still don't have destination
                if (destinationAddress == null)
                {
                    var fields = instanceType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    foreach (var field in fields)
                    {
                        Log.Debug("MassTransit SendEndpointPipeSendIntegration - Available field: {FieldName} of type {FieldType}", field.Name, field.FieldType.FullName);
                    }
                }
            }

            // Try to get message type from context's generic argument or SupportedMessageTypes
            var messageTypesProperty = contextType.GetProperty("SupportedMessageTypes");
            if (messageTypesProperty?.GetValue(context) is System.Collections.IEnumerable messageTypes)
            {
                foreach (var mt in messageTypes)
                {
                    messageType = mt?.ToString();
                    break; // Just take the first one
                }
            }

            // If we couldn't get message type from SupportedMessageTypes, try the generic type argument
            if (string.IsNullOrEmpty(messageType) && contextType.IsGenericType)
            {
                var genericArgs = contextType.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    messageType = genericArgs[0].Name;
                    Log.Debug("MassTransit SendEndpointPipeSendIntegration - Got message type from generic: {MessageType}", messageType);
                }
            }

            // Get the Headers property directly from the context using reflection
            var headersProperty = contextType.GetProperty("Headers");
            if (headersProperty == null)
            {
                Log.Warning("MassTransit SendEndpointPipeSendIntegration - Headers property not found on context type: {ContextType}", contextType.FullName);
                return CallTargetState.GetDefault();
            }

            headersObj = headersProperty.GetValue(context);
            if (headersObj == null)
            {
                Log.Warning("MassTransit SendEndpointPipeSendIntegration - Headers property is null");
                return CallTargetState.GetDefault();
            }

            Log.Debug("MassTransit SendEndpointPipeSendIntegration - Headers type: {HeadersType}", headersObj.GetType().FullName);

            // Use reflection to call the Set method directly on headers
            var headersType = headersObj.GetType();
            setMethod = headersType.GetMethod("Set", new[] { typeof(string), typeof(object), typeof(bool) });

            if (setMethod == null)
            {
                var sendHeadersInterface = headersType.GetInterface("MassTransit.SendHeaders");
                if (sendHeadersInterface != null)
                {
                    setMethod = sendHeadersInterface.GetMethod("Set", new[] { typeof(string), typeof(object), typeof(bool) });
                }
            }

            if (setMethod == null)
            {
                Log.Warning("MassTransit SendEndpointPipeSendIntegration - Set method not found on headers type: {HeadersType}", headersType.FullName);
                return CallTargetState.GetDefault();
            }

            // Create the send span - this matches MT8 OTEL behavior
            scope = MassTransitIntegration.CreateProducerScope(
                tracer,
                MassTransitConstants.OperationSend,
                messageType,
                destinationName: destinationAddress,
                messagingSystem: messagingSystem);

            if (scope != null)
            {
                Log.Debug("MassTransit SendEndpointPipeSendIntegration - Created send span for destination: {Destination}", destinationAddress);

                // Set additional tags
                if (scope.Span?.Tags is MassTransitTags tags)
                {
                    // Extract additional context info
                    // Handle nullable Guid properties - they may be Guid? which won't match "is Guid" when null
                    var messageIdProp = contextType.GetProperty("MessageId");
                    if (messageIdProp != null)
                    {
                        var messageIdValue = messageIdProp.GetValue(context);
                        if (messageIdValue is Guid messageId && messageId != Guid.Empty)
                        {
                            tags.MessageId = messageId.ToString();
                        }
                    }

                    var conversationIdProp = contextType.GetProperty("ConversationId");
                    if (conversationIdProp != null)
                    {
                        var conversationIdValue = conversationIdProp.GetValue(context);
                        if (conversationIdValue is Guid conversationId && conversationId != Guid.Empty)
                        {
                            tags.ConversationId = conversationId.ToString();
                        }
                    }

                    var correlationIdProp = contextType.GetProperty("CorrelationId");
                    if (correlationIdProp != null)
                    {
                        var correlationIdValue = correlationIdProp.GetValue(context);
                        if (correlationIdValue is Guid correlationId && correlationId != Guid.Empty)
                        {
                            tags.CorrelationId = correlationId.ToString();
                        }
                    }

                    var sourceAddressProp = contextType.GetProperty("SourceAddress");
                    if (sourceAddressProp?.GetValue(context) is Uri sourceAddress)
                    {
                        tags.SourceAddress = sourceAddress.ToString();
                    }

                    // MT8 OTEL tag: request_id
                    var requestIdProp = contextType.GetProperty("RequestId");
                    if (requestIdProp != null)
                    {
                        var requestIdValue = requestIdProp.GetValue(context);
                        if (requestIdValue is Guid requestId && requestId != Guid.Empty)
                        {
                            tags.RequestId = requestId.ToString();
                        }
                    }

                    // MT8 OTEL tag: body size - ContentLength/BodyLength may not be available
                    // until the message body is serialized, so wrap in try/catch
                    try
                    {
                        var contentLengthProp = contextType.GetProperty("ContentLength");
                        if (contentLengthProp != null)
                        {
                            var contentLength = contentLengthProp.GetValue(context);
                            if (contentLength != null)
                            {
                                var lengthStr = contentLength.ToString();
                                if (!string.IsNullOrEmpty(lengthStr) && lengthStr != "0")
                                {
                                    tags.MessageSize = lengthStr;
                                }
                            }
                        }

                        // Also try BodyLength as fallback
                        if (string.IsNullOrEmpty(tags.MessageSize))
                        {
                            var bodyLengthProp = contextType.GetProperty("BodyLength");
                            if (bodyLengthProp != null)
                            {
                                var bodyLength = bodyLengthProp.GetValue(context);
                                if (bodyLength != null)
                                {
                                    var lengthStr = bodyLength.ToString();
                                    if (!string.IsNullOrEmpty(lengthStr) && lengthStr != "0")
                                    {
                                        tags.MessageSize = lengthStr;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // BodyLength/ContentLength may throw if message body hasn't been serialized yet
                        // This is expected - skip setting the body size tag
                    }

                    // Set peer address to match MT8 OTEL
                    if (destinationAddress != null)
                    {
                        tags.PeerAddress = destinationAddress;
                    }
                }

                // Now inject trace context with the NEW span context (send span is the parent)
                var spanContext = scope.Span?.Context as SpanContext;
                if (spanContext != null)
                {
                    var headersAdapter = new ReflectionSendHeadersAdapter(headersObj, setMethod);
                    var propagationContext = new PropagationContext(spanContext, Baggage.Current);
                    tracer.TracerManager.SpanContextPropagator.Inject(propagationContext, headersAdapter);
                    Log.Debug("MassTransit SendEndpointPipeSendIntegration - Injected trace context. TraceId: {TraceId}, SpanId: {SpanId}", spanContext.TraceId, spanContext.SpanId);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MassTransit SendEndpointPipeSendIntegration - Failed to create send span or inject headers");
        }

        return new CallTargetState(scope);
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value</returns>
    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        Log.Debug("MassTransit SendEndpointPipeSendIntegration.OnAsyncMethodEnd() - Completing send span");

        if (exception != null)
        {
            Log.Warning(exception, "MassTransit SendEndpointPipeSendIntegration - Send failed with exception");
        }

        state.Scope.DisposeWithException(exception);
        return returnValue;
    }

    private static string DetermineMessagingSystem(string? destination)
    {
        if (string.IsNullOrEmpty(destination))
        {
            return "in-memory";
        }

        if (destination!.IndexOf("rabbitmq://", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "rabbitmq";
        }

        if (destination.IndexOf("sb://", StringComparison.OrdinalIgnoreCase) >= 0 ||
            destination.IndexOf("servicebus", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "azureservicebus";
        }

        if (destination.IndexOf("amazonsqs://", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "amazonsqs";
        }

        if (destination.IndexOf("kafka://", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "kafka";
        }

        if (destination.IndexOf("loopback://", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "in-memory";
        }

        return "in-memory";
    }
}
