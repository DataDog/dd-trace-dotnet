// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Serverless;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;

/// <summary>
/// System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]] Azure.Messaging.ServiceBus.ServiceBusReceiver::ReceiveMessagesAsync(System.Int32,System.Nullable`1[System.TimeSpan],System.Boolean,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusReceiver",
    MethodName = "ReceiveMessagesAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]]",
    ParameterTypeNames = [ClrNames.Int32, "System.Nullable`1[System.TimeSpan]", ClrNames.Bool, ClrNames.CancellationToken],
    MinimumVersion = "7.14.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ServiceBusReceiverReceiveMessagesAsyncIntegration
{
    private const string OperationName = "azure_servicebus.receive";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusReceiverReceiveMessagesAsyncIntegration));
    private static readonly object ProcessorReceiveState = new();

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int maxMessages, TimeSpan? maxWaitTime, bool isProcessor, CancellationToken cancellationToken)
        where TTarget : IServiceBusReceiver, IDuckType
    {
        var receiveState = isProcessor ? ProcessorReceiveState : null;
        return new CallTargetState(scope: null, state: receiveState);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
        where TTarget : IServiceBusReceiver, IDuckType
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
        {
            return returnValue;
        }

        var messagesList = returnValue as System.Collections.IList;
        var messageCount = messagesList?.Count ?? 0;

        // Don't create spans when there's an exception or no messages
        if (exception != null || messageCount == 0)
        {
            return returnValue;
        }

        var spanLinks = ExtractSpanLinksFromMessages(tracer, messagesList);
        var scope = CreateAndConfigureSpan(tracer, spanLinks, instance, messagesList);

        var isProcessorReceive = ReferenceEquals(state.State, ProcessorReceiveState);
        var shouldReinjectContext = ShouldReinjectContext(
            tracer.Settings.IsRunningInAzureFunctions,
            isProcessorReceive,
            AzureInfo.Instance.IsIsolatedFunctionHostProcess);

        // Re-inject the new span context into all messages so the Azure Functions trigger handoff
        // parents to this receive span. See ShouldReinjectContext for why processor receives are
        // excluded outside the isolated Functions host process.
        if (scope != null && messagesList != null && messageCount > 0 && shouldReinjectContext)
        {
            ReinjectContextIntoMessages(tracer, scope, messagesList);
        }

        if (scope != null)
        {
            scope.Dispose();
        }

        return returnValue;
    }

    // Decides whether the receive-span context should be written back into the received messages so a
    // downstream reader parents to it. This is only wanted for the Azure Functions Service Bus trigger
    // handoff, and only where the function invocation is parented by reading the message:
    //   - Isolated model: the host process receives and serializes the message over gRPC; the worker
    //     parents its function span by extracting the context from UserProperties (see
    //     AzureFunctionsCommon.CreateIsolatedFunctionScope), so the host must reinject.
    //   - Non-processor receives in Functions: kept for back-compat with manual receives.
    //
    // It must NOT run for a user-created ServiceBusProcessor: with the Azure activity source enabled the
    // SDK's ServiceBusProcessor.ProcessMessage activity parents to the message context, so overwriting it
    // here splits the producer and consumer into separate traces.
    //
    // The in-process Functions trigger is intentionally excluded too: its function span is created at
    // FunctionExecutor.TryExecuteAsync and parents to the active scope, not by reading the message, so it
    // does not depend on reinjection. Do not re-enable it for that case.
    internal static bool ShouldReinjectContext(bool isRunningInAzureFunctions, bool isProcessorReceive, bool isIsolatedFunctionHostProcess)
        => isRunningInAzureFunctions && (!isProcessorReceive || isIsolatedFunctionHostProcess);

    private static List<SpanLink>? ExtractSpanLinksFromMessages(Tracer tracer, System.Collections.IList? messagesList)
    {
        if (messagesList == null || messagesList.Count == 0)
        {
            return null;
        }

        if (!tracer.Settings.AzureServiceBusBatchLinksEnabled)
        {
            return null;
        }

        var extractedContexts = new HashSet<SpanContext>(new SpanContextComparer());

        try
        {
            foreach (var message in messagesList)
            {
                if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true &&
                    serviceBusMessage.ApplicationProperties != null)
                {
                    var extractedContext = AzureMessagingCommon.ExtractContext(serviceBusMessage.ApplicationProperties);
                    if (extractedContext.SpanContext != null)
                    {
                        extractedContexts.Add(extractedContext.SpanContext);
                    }
                }
            }

            var spanLinks = new List<SpanLink>(extractedContexts.Count);

            foreach (var ctx in extractedContexts)
            {
                spanLinks.Add(new SpanLink(ctx));
            }

            return spanLinks;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ServiceBusReceiver: Error extracting contexts from ServiceBus messages");
        }

        return null;
    }

    private static Scope? CreateAndConfigureSpan<TTarget>(
        Tracer tracer,
        IEnumerable<SpanLink>? spanLinks,
        TTarget receiverInstance,
        System.Collections.IList? messagesList)
        where TTarget : IServiceBusReceiver
    {
        var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureServiceBusTags(SpanKinds.Consumer);

        var entityPath = receiverInstance.EntityPath ?? "unknown";
        tags.MessagingDestinationName = entityPath;
        tags.MessagingOperation = "receive";
        tags.MessagingSystem = "servicebus";

        var (serviceName, serviceNameSource) = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.AzureServiceBus);
        var scope = tracer.StartActiveInternal(
            OperationName,
            links: spanLinks,
            tags: tags,
            serviceName: serviceName,
            serviceNameSource: serviceNameSource);
        var span = scope.Span;

        span.Type = SpanTypes.Queue;
        span.ResourceName = entityPath;

        if (messagesList?.Count > 1)
        {
            span.SetTag(Tags.MessagingBatchMessageCount, messagesList.Count.ToString());
        }

        // Set MessagingMessageId if single message received
        if (messagesList?.Count == 1)
        {
            var message = messagesList[0];
            if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true)
            {
                var messageId = serviceBusMessage.MessageId;
                if (!string.IsNullOrEmpty(messageId))
                {
                    span.SetTag(Tags.MessagingMessageId, messageId);
                }
            }
        }

        var endpoint = receiverInstance.Connection?.ServiceEndpoint;
        if (endpoint != null)
        {
            tags.ServerAddress = endpoint.Host;
        }

        tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureServiceBus);

        return scope;
    }

    private static void ReinjectContextIntoMessages(Tracer tracer, Scope scope, System.Collections.IList messagesList)
    {
        try
        {
            foreach (var message in messagesList)
            {
                if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true)
                {
                    var amqpMessage = serviceBusMessage.AmqpMessage;
                    if (amqpMessage?.ApplicationProperties != null)
                    {
                        AzureMessagingCommon.InjectContext(amqpMessage.ApplicationProperties, scope);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ServiceBusReceiver: Error re-injecting context into ServiceBus messages");
        }
    }
}
