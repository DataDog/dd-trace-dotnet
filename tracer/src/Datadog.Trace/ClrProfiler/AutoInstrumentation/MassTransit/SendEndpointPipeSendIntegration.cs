// <copyright file="SendEndpointPipeSendIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
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
        if (context is null)
        {
            return CallTargetState.GetDefault();
        }

        var tracer = Tracer.Instance;
        Scope? scope = null;
        string? destinationAddress = null;
        string? messageType = null;
        string messagingSystem = "in-memory";
        ISendContext? duckContext = null;
        object? headersObj = null;

        try
        {
            var contextType = context.GetType();

            // The SendEndpointPipe is a nested class inside SendEndpoint.
            // The DestinationAddress on context is null at OnMethodBegin because it gets set
            // inside the Send method: context.DestinationAddress = _endpoint.DestinationAddress
            // We need to get the destination from the outer SendEndpoint class instead.
            try
            {
                var instanceType = instance?.GetType();
                if (instanceType != null)
                {
                    // SendEndpointPipe has _endpoint field pointing to the outer SendEndpoint
                    var endpointField = instanceType.GetField("_endpoint", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (endpointField != null)
                    {
                        var endpoint = endpointField.GetValue(instance);
                        if (endpoint != null)
                        {
                            var endpointType = endpoint.GetType();

                            // Try to get DestinationAddress - first check direct property
                            var destProp = endpointType.GetProperty("DestinationAddress", BindingFlags.Public | BindingFlags.Instance);

                            if (destProp == null)
                            {
                                // Try from interface
                                var sendEndpointInterface = endpointType.GetInterface("MassTransit.ISendEndpoint");
                                if (sendEndpointInterface != null)
                                {
                                    destProp = sendEndpointInterface.GetProperty("DestinationAddress");
                                }
                            }

                            if (destProp == null)
                            {
                                // Try getting the backing field directly (auto-property backing field)
                                var destField = endpointType.GetField("<DestinationAddress>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (destField?.GetValue(endpoint) is Uri fieldUri)
                                {
                                    destinationAddress = fieldUri.ToString();
                                    messagingSystem = DetermineMessagingSystem(destinationAddress);
                                }
                            }
                            else if (destProp.GetValue(endpoint) is Uri destUri)
                            {
                                destinationAddress = destUri.ToString();
                                messagingSystem = DetermineMessagingSystem(destinationAddress);
                            }
                        }
                    }
                }
            }
            catch (Exception endpointEx)
            {
                Log.Debug(endpointEx, "MassTransit SendEndpointPipeSendIntegration - Failed to get destination from _endpoint");
            }

            // Try to duck-cast the context to access its properties
            if (context.TryDuckCast<ISendContext>(out var ducked))
            {
                duckContext = ducked;
                headersObj = duckContext.Headers;
            }
            else
            {
                // Fall back to reflection for headers
                var headersProperty = contextType.GetProperty("Headers");
                headersObj = headersProperty?.GetValue(context);
            }

            // Get message type from the generic type argument
            if (string.IsNullOrEmpty(messageType))
            {
                if (contextType.IsGenericType)
                {
                    var genericArgs = contextType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        messageType = genericArgs[0].Name;
                    }
                }
            }

            if (headersObj == null)
            {
                Log.Warning("MassTransit SendEndpointPipeSendIntegration - Headers property is null");
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
                // Set additional tags from duck-typed context
                if (scope.Span?.Tags is MassTransitTags tags)
                {
                    if (duckContext != null)
                    {
                        try
                        {
                            if (duckContext.MessageId.HasValue && duckContext.MessageId.Value != Guid.Empty)
                            {
                                tags.MessageId = duckContext.MessageId.Value.ToString();
                            }

                            if (duckContext.ConversationId.HasValue && duckContext.ConversationId.Value != Guid.Empty)
                            {
                                tags.ConversationId = duckContext.ConversationId.Value.ToString();
                            }

                            if (duckContext.CorrelationId.HasValue && duckContext.CorrelationId.Value != Guid.Empty)
                            {
                                tags.CorrelationId = duckContext.CorrelationId.Value.ToString();
                            }

                            if (duckContext.SourceAddress != null)
                            {
                                tags.SourceAddress = duckContext.SourceAddress.ToString();
                            }

                            if (duckContext.RequestId.HasValue && duckContext.RequestId.Value != Guid.Empty)
                            {
                                tags.RequestId = duckContext.RequestId.Value.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed to set additional tags from duck-typed context");
                        }
                    }

                    // Set peer address to match MT8 OTEL
                    if (destinationAddress != null)
                    {
                        tags.PeerAddress = destinationAddress;
                    }
                }

                // Now inject trace context with the NEW span context (send span is the parent)
                var spanContext = scope.Span?.Context as SpanContext;
                if (spanContext != null && headersObj != null)
                {
                    InjectTraceContext(headersObj, spanContext, tracer);
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
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }

    private static void InjectTraceContext(object headersObj, SpanContext spanContext, Tracer tracer)
    {
        // Try to duck-cast headers to ISendHeaders for injection
        if (headersObj.TryDuckCast<ISendHeaders>(out var duckHeaders))
        {
            var headersAdapter = new SendContextPropagation(duckHeaders);
            var propagationContext = new PropagationContext(spanContext, Baggage.Current);
            tracer.TracerManager.SpanContextPropagator.Inject(propagationContext, headersAdapter);
        }
        else
        {
            // Fallback to reflection if duck typing fails
            var headersType = headersObj.GetType();
            var setMethod = headersType.GetMethod("Set", new[] { typeof(string), typeof(object), typeof(bool) });

            if (setMethod == null)
            {
                var sendHeadersInterface = headersType.GetInterface("MassTransit.SendHeaders");
                if (sendHeadersInterface != null)
                {
                    setMethod = sendHeadersInterface.GetMethod("Set", new[] { typeof(string), typeof(object), typeof(bool) });
                }
            }

            if (setMethod != null)
            {
                var adapter = new ReflectionSendHeadersAdapter(headersObj, setMethod);
                var propagationContext = new PropagationContext(spanContext, Baggage.Current);
                tracer.TracerManager.SpanContextPropagator.Inject(propagationContext, adapter);
            }
            else
            {
                Log.Warning("MassTransit SendEndpointPipeSendIntegration - Could not inject trace context: Set method not found on headers type: {HeadersType}", headersType.FullName);
            }
        }
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

    /// <summary>
    /// Simple adapter for reflection-based header injection (fallback when duck typing fails)
    /// </summary>
    private readonly struct ReflectionSendHeadersAdapter : Headers.IHeadersCollection
    {
        private readonly object _headers;
        private readonly MethodInfo _setMethod;

        public ReflectionSendHeadersAdapter(object headers, MethodInfo setMethod)
        {
            _headers = headers;
            _setMethod = setMethod;
        }

        public System.Collections.Generic.IEnumerable<string> GetValues(string name)
        {
            yield break; // Write-only adapter
        }

        public void Set(string name, string value)
        {
            _setMethod.Invoke(_headers, new object[] { name, value, true });
        }

        public void Add(string name, string value)
        {
            _setMethod.Invoke(_headers, new object[] { name, value, true });
        }

        public void Remove(string name)
        {
            // Not supported
        }
    }
}
