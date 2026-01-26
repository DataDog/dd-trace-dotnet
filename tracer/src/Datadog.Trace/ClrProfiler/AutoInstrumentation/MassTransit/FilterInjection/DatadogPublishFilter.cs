// <copyright file="DatadogPublishFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.FilterInjection;

/// <summary>
/// A MassTransit filter that instruments publish operations for Datadog tracing.
/// This implements GreenPipes.IFilter{PublishContext} for MassTransit 7.x.
///
/// The publish filter creates a producer span that encompasses the entire publish operation,
/// including the actual transport call (RabbitMQ, SQS, etc.). This ensures that
/// transport-level spans become children of the MassTransit publish span.
/// </summary>
internal sealed class DatadogPublishFilter
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogPublishFilter));

    /// <summary>
    /// Creates a probe scope for diagnostic purposes.
    /// Called by MassTransit's pipeline probing mechanism.
    /// </summary>
    /// <param name="context">The probe context</param>
    [DuckReverseMethod(ParameterTypeNames = ["GreenPipes.ProbeContext, GreenPipes"])]
    public void Probe(object context)
    {
        try
        {
            var contextType = context.GetType();
            var createScopeMethod = contextType.GetMethod("CreateFilterScope", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            createScopeMethod?.Invoke(context, new object[] { "datadog-publish" });
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogPublishFilter: Failed to create probe scope");
        }
    }

    /// <summary>
    /// Processes the publish context through the filter pipeline.
    /// Creates a Datadog "publish" span that encompasses the entire publish operation,
    /// including the actual transport call.
    /// </summary>
    /// <param name="context">The publish context</param>
    /// <param name="next">The next pipe in the pipeline</param>
    /// <returns>A task representing the async operation</returns>
    [DuckReverseMethod(ParameterTypeNames = ["MassTransit.PublishContext, MassTransit", "GreenPipes.IPipe`1[MassTransit.PublishContext], GreenPipes"])]
    public async Task Send(object? context, object next)
    {
        Log.Debug<string?>("DatadogPublishFilter.Send() - Processing publish context: {ContextType}", context?.GetType().Name);

        var tracer = Tracer.Instance;
        Scope? publishScope = null;

        try
        {
            // Extract message info from the publish context
            ExtractMessageInfo(context, out var messageType, out var destinationName, out var messagingSystem);

            // Create the producer/publish span
            publishScope = MassTransitIntegration.CreateProducerScope(
                tracer,
                MassTransitConstants.OperationPublish,
                messageType,
                destinationName: destinationName,
                messagingSystem: messagingSystem);

            if (publishScope != null && context != null)
            {
                // Set publish-specific tags from the publish context
                SetPublishContextTags(publishScope, context);

                // Inject trace context into headers for propagation
                InjectTraceContext(context, publishScope.Span.Context, tracer);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogPublishFilter: Failed to create publish scope");
        }

        try
        {
            // Call next.Send(context) to continue the pipeline
            // This is where the actual transport publish happens
            await InvokeNextPipe(next, context!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            publishScope?.Span?.SetException(ex);
            throw;
        }
        finally
        {
            publishScope?.Dispose();
        }
    }

    private static void ExtractMessageInfo(object? context, out string? messageType, out string? destinationName, out string? messagingSystem)
    {
        messageType = null;
        destinationName = null;
        messagingSystem = "in-memory";

        if (context == null)
        {
            return;
        }

        try
        {
            // PublishContext inherits from SendContext, so we can use ISendContext for duck typing
            if (context.TryDuckCast<ISendContext>(out var duckContext))
            {
                // Get destination from DestinationAddress
                var destAddress = duckContext.DestinationAddress;
                if (destAddress != null)
                {
                    destinationName = destAddress.ToString();

                    // Extract just the queue/topic name from the full address using common helper
                    messageType = MassTransitIntegration.ExtractDestinationName(destinationName);

                    // Determine messaging system from address
                    messagingSystem = DetermineMessagingSystem(destinationName);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogPublishFilter: Failed to extract message info");
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

    private static void SetPublishContextTags(Scope scope, object context)
    {
        if (scope.Span?.Tags is not MassTransitTags tags)
        {
            return;
        }

        try
        {
            // PublishContext inherits from SendContext
            if (context.TryDuckCast<ISendContext>(out var duckContext))
            {
                // Set publish context tags similar to send context tags
                tags.MessageId = duckContext.MessageId?.ToString();
                tags.ConversationId = duckContext.ConversationId?.ToString();
                tags.CorrelationId = duckContext.CorrelationId?.ToString();
                tags.SourceAddress = duckContext.SourceAddress?.ToString();
                tags.RequestId = duckContext.RequestId?.ToString();

                var destAddress = duckContext.DestinationAddress?.ToString();
                if (!string.IsNullOrEmpty(destAddress))
                {
                    tags.DestinationAddress = destAddress;
                    tags.PeerAddress = destAddress;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogPublishFilter: Failed to set publish context tags");
        }
    }

    private static void InjectTraceContext(object context, SpanContext spanContext, Tracer tracer)
    {
        try
        {
            // PublishContext inherits from SendContext
            if (context.TryDuckCast<ISendContext>(out var duckContext))
            {
                var headersObj = duckContext.Headers;
                if (headersObj != null)
                {
                    // Use reflection to inject trace context into MassTransit headers.
                    var headersType = headersObj.GetType();
                    var setMethod = headersType.GetMethod("Set", [typeof(string), typeof(object), typeof(bool)]);

                    if (setMethod == null)
                    {
                        var sendHeadersInterface = headersType.GetInterface("MassTransit.SendHeaders");
                        if (sendHeadersInterface != null)
                        {
                            setMethod = sendHeadersInterface.GetMethod("Set", [typeof(string), typeof(object), typeof(bool)]);
                        }
                    }

                    if (setMethod != null)
                    {
                        // Use the Action-based overload for injection
                        var propagationContext = new PropagationContext(spanContext, Baggage.Current);
                        var carrier = new HeaderInjectionCarrier(headersObj, setMethod);
                        tracer.TracerManager.SpanContextPropagator.Inject(
                            propagationContext,
                            carrier,
                            HeaderInjectionCarrier.Setter);
                        Log.Debug("DatadogPublishFilter: Successfully injected trace context into headers");
                    }
                    else
                    {
                        Log.Warning("DatadogPublishFilter: Could not find Set method on headers type: {HeadersType}", headersType.FullName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogPublishFilter: Failed to inject trace context");
        }
    }

    private static async Task InvokeNextPipe(object next, object context)
    {
        var nextType = next.GetType();

        // Try to find the Send method
        var sendMethod = nextType.GetMethod("Send", BindingFlags.Public | BindingFlags.Instance);

        if (sendMethod == null)
        {
            foreach (var method in nextType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name == "Send" || method.Name.EndsWith(".Send"))
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1)
                    {
                        sendMethod = method;
                        Log.Debug<string>("DatadogPublishFilter: Found Send method: {MethodName}", method.Name);
                        break;
                    }
                }
            }
        }

        if (sendMethod == null)
        {
            // Try interface mapping for IPipe<T>.Send
            foreach (var iface in nextType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.Name.StartsWith("IPipe"))
                {
                    var map = nextType.GetInterfaceMap(iface);
                    for (int i = 0; i < map.InterfaceMethods.Length; i++)
                    {
                        if (map.InterfaceMethods[i].Name == "Send")
                        {
                            sendMethod = map.TargetMethods[i];
                            Log.Debug<string>("DatadogPublishFilter: Found Send method via interface map: {MethodName}", sendMethod.Name);
                            break;
                        }
                    }

                    if (sendMethod != null)
                    {
                        break;
                    }
                }
            }
        }

        if (sendMethod == null)
        {
            Log.Warning<string>("DatadogPublishFilter: Could not find Send method on next pipe. Type: {NextType}", nextType.FullName ?? "unknown");
            return;
        }

        var task = sendMethod.Invoke(next, new[] { context }) as Task;
        if (task != null)
        {
            await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Carrier struct for injecting headers into MassTransit SendHeaders.
    /// This avoids using ValueTuple which is not available on .NET Framework 4.6.1.
    /// </summary>
    private readonly struct HeaderInjectionCarrier
    {
        public static readonly Action<HeaderInjectionCarrier, string, string> Setter = SetHeader;

        private readonly object _headers;
        private readonly MethodInfo _setMethod;

        public HeaderInjectionCarrier(object headers, MethodInfo setMethod)
        {
            _headers = headers;
            _setMethod = setMethod;
        }

        private static void SetHeader(HeaderInjectionCarrier carrier, string key, string value)
        {
            try
            {
                carrier._setMethod.Invoke(carrier._headers, new object[] { key, value, true });
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "DatadogPublishFilter: Failed to set header {Key}", key);
            }
        }
    }
}
