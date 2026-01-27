// <copyright file="DatadogConsumeFilter.cs" company="Datadog">
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
/// A MassTransit filter that instruments consume operations for Datadog tracing.
/// This implements GreenPipes.IFilter{ConsumeContext} for MassTransit 7.x.
///
/// Note: This filter creates spans for consume operations and extracts trace context
/// from message headers for distributed tracing.
/// </summary>
public sealed class DatadogConsumeFilter
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DatadogConsumeFilter>();

    /// <summary>
    /// Creates a probe scope for diagnostic purposes.
    /// Called by MassTransit's pipeline probing mechanism.
    /// </summary>
    /// <param name="context">The probe context</param>
    [DuckReverseMethod(ParameterTypeNames = new[] { "GreenPipes.ProbeContext, GreenPipes" })]
    public void Probe(object context)
    {
        try
        {
            // Try to call context.CreateFilterScope("datadog")
            var contextType = context.GetType();
            var createScopeMethod = contextType.GetMethod("CreateFilterScope", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            createScopeMethod?.Invoke(context, new object[] { "datadog" });
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogConsumeFilter: Failed to create probe scope");
        }
    }

    /// <summary>
    /// Processes the consume context through the filter pipeline.
    /// Creates a Datadog "process" span for the consumer execution.
    ///
    /// Note: The "receive" span is created by ReceivePipeDispatcherIntegration which instruments
    /// the receive pipeline at a higher level. This filter only creates the "process" span
    /// which wraps the actual consumer business logic, matching MT8 OTEL behavior.
    /// </summary>
    /// <param name="context">The consume context</param>
    /// <param name="next">The next pipe in the pipeline</param>
    /// <returns>A task representing the async operation</returns>
    [DuckReverseMethod(ParameterTypeNames = new[] { "MassTransit.ConsumeContext, MassTransit", "GreenPipes.IPipe`1[MassTransit.ConsumeContext], GreenPipes" })]
    public async Task Send(object? context, object next)
    {
        Log.Debug<string?>("DatadogConsumeFilter.Send() - Processing consume context: {ContextType}", context?.GetType().Name);

        var tracer = Tracer.Instance;
        Scope? processScope = null;

        try
        {
            // Extract message info from the consume context
            // Note: We don't extract propagation context here because the parent span (receive)
            // is already active on the ambient context from ReceivePipeDispatcherIntegration
            ExtractMessageInfo(context, out var messageType, out var inputAddress, out var destinationName, out var messagingSystem);

            // Create the "process" span (MT8 OTEL: "{destination} process")
            // This span is automatically a child of the active receive span
            processScope = MassTransitIntegration.CreateConsumerScope(
                tracer,
                MassTransitConstants.OperationProcess,
                messageType,
                destinationName: inputAddress ?? destinationName,
                messagingSystem: messagingSystem);

            if (processScope != null)
            {
                // Set process-specific tags from the consume context
                SetConsumeContextTags(processScope, context);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogConsumeFilter: Failed to create process scope");
        }

        try
        {
            // Call next.Send(context) to continue the pipeline
            await InvokeNextPipe(next, context!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            processScope?.Span?.SetException(ex);
            throw;
        }
        finally
        {
            processScope?.Dispose();
        }
    }

    private static PropagationContext ExtractPropagationContext(object? context)
    {
        if (context == null)
        {
            return default;
        }

        try
        {
            // Try to duck-cast to IConsumeContext
            if (context.TryDuckCast<IConsumeContext>(out var duckContext))
            {
                var headersObj = duckContext.Headers;
                if (headersObj != null)
                {
                    var headersAdapter = new ContextPropagation(headersObj);
                    return Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headersAdapter);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogConsumeFilter: Failed to extract propagation context");
        }

        return default;
    }

    private static void ExtractMessageInfo(object? context, out string? messageType, out string? inputAddress, out string? destinationName, out string? messagingSystem)
    {
        messageType = null;
        inputAddress = null;
        destinationName = null;
        messagingSystem = "in-memory";

        if (context == null)
        {
            return;
        }

        try
        {
            // The context may be MessageConsumeContext<T> which wraps the actual ConsumeContext
            // First try to get the inner _context if this is a MessageConsumeContext wrapper
            object? innerContext = context;
            if (context.TryDuckCast<IMessageConsumeContext>(out var messageConsumeContext))
            {
                innerContext = messageConsumeContext.Context;
            }

            // Now try to duck-cast to IConsumeContext
            if (innerContext != null && innerContext.TryDuckCast<IConsumeContext>(out var duckContext))
            {
                // Get message type from SupportedMessageTypes
                foreach (var supportedType in duckContext.SupportedMessageTypes)
                {
                    // Format is typically "urn:message:Namespace:MessageType"
                    if (!string.IsNullOrEmpty(supportedType))
                    {
                        var lastColon = supportedType.LastIndexOf(':');
                        if (lastColon > 0 && lastColon < supportedType.Length - 1)
                        {
                            messageType = supportedType.Substring(lastColon + 1);
                        }
                        else
                        {
                            messageType = supportedType;
                        }

                        break;
                    }
                }

                // Get InputAddress from ReceiveContext (MT8 OTEL: the actual queue name)
                // This is used for the resource name in receive/process spans
                try
                {
                    var receiveContextObj = duckContext.ReceiveContext;
                    if (receiveContextObj != null && receiveContextObj.TryDuckCast<IReceiveContext>(out var receiveContext))
                    {
                        inputAddress = receiveContext.InputAddress?.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "DatadogConsumeFilter: Failed to extract InputAddress from ReceiveContext");
                }

                // Get destination from DestinationAddress
                var destAddress = duckContext.DestinationAddress;
                if (destAddress != null)
                {
                    destinationName = destAddress.ToString();

                    // Determine messaging system from address
                    var addressToCheck = inputAddress ?? destinationName;
                    if (addressToCheck.IndexOf("rabbitmq://", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        messagingSystem = "rabbitmq";
                    }
                    else if (addressToCheck.IndexOf("sb://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             addressToCheck.IndexOf("servicebus", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        messagingSystem = "azureservicebus";
                    }
                    else if (addressToCheck.IndexOf("amazonsqs://", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        messagingSystem = "amazonsqs";
                    }
                    else if (addressToCheck.IndexOf("kafka://", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        messagingSystem = "kafka";
                    }
                    else if (addressToCheck.IndexOf("loopback://", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        messagingSystem = "in-memory";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogConsumeFilter: Failed to extract message info");
        }
    }

    private static void SetConsumeContextTags(Scope scope, object? context)
    {
        if (context == null || scope.Span?.Tags is not MassTransitTags tags)
        {
            return;
        }

        try
        {
            if (context.TryDuckCast<IConsumeContext>(out var duckContext))
            {
                MassTransitIntegration.SetConsumeContextTags(tags, duckContext);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogConsumeFilter: Failed to set consume context tags");
        }
    }

    private static async Task InvokeNextPipe(object next, object context)
    {
        // Call next.Send(context) using reflection
        // The 'next' parameter is IPipe<ConsumeContext> which has a Send method
        var nextType = next.GetType();

        // Try to find the Send method - it may be explicitly implemented
        var sendMethod = nextType.GetMethod("Send", BindingFlags.Public | BindingFlags.Instance);

        if (sendMethod == null)
        {
            // Try to find any method that matches the pattern for Send
            foreach (var method in nextType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name == "Send" || method.Name.EndsWith(".Send"))
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1)
                    {
                        sendMethod = method;
                        Log.Debug<string>("DatadogConsumeFilter: Found Send method: {MethodName}", method.Name);
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
                            Log.Debug<string>("DatadogConsumeFilter: Found Send method via interface map: {MethodName}", sendMethod.Name);
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
            Log.Warning<string>("DatadogConsumeFilter: Could not find Send method on next pipe. Type: {NextType}", nextType.FullName ?? "unknown");
            return;
        }

        var task = sendMethod.Invoke(next, new[] { context }) as Task;
        if (task != null)
        {
            await task.ConfigureAwait(false);
        }
    }
}
