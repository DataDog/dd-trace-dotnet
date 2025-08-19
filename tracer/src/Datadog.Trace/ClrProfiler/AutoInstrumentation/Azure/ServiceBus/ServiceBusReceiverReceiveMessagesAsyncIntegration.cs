// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

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
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ServiceBusReceiverReceiveMessagesAsyncIntegration
{
    private const string OperationName = "servicebus.receive";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusReceiverReceiveMessagesAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int maxMessages, TimeSpan? maxWaitTime, bool isProcessor, CancellationToken cancellationToken)
        where TTarget : IServiceBusReceiver, IDuckType
    {
        Log.Information("ServiceBusReceiverReceiveMessagesAsyncIntegration.OnMethodBegin called");
        var tracer = Tracer.Instance;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
        {
            return CallTargetState.GetDefault();
        }

        var startTime = DateTimeOffset.UtcNow;
        return new CallTargetState(null, new ReceiveMessagesState(instance, startTime));
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Log.Information("ServiceBusReceiverReceiveMessagesAsyncIntegration.OnAsyncMethodEnd called");

        var tracer = Tracer.Instance;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus) ||
            !(state.State is ReceiveMessagesState stateData))
        {
            return returnValue;
        }

        var receiverInstance = stateData.Instance;
        var startTime = stateData.StartTime;
        var messagesList = returnValue as System.Collections.IList;
        var messageCount = messagesList?.Count ?? 0;

        if (exception == null && messageCount == 0)
        {
            return returnValue;
        }

        var parentContext = ExtractParentContextFromFirstMessage(tracer, messagesList);
        var scope = CreateAndConfigureSpan(tracer, parentContext, receiverInstance, startTime, exception);

        // Re-inject the new span context into all messages so Azure Functions will use it as parent
        if (scope != null && messagesList != null && messageCount > 0)
        {
            ReinjectContextIntoMessages(tracer, scope, messagesList);
        }

        return returnValue;
    }

    private static SpanContext? ExtractParentContextFromFirstMessage(Tracer tracer, System.Collections.IList? messagesList)
    {
        if (messagesList == null || messagesList.Count == 0)
        {
            return null;
        }

        try
        {
            var firstMessage = messagesList[0];
            if (firstMessage?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true &&
                serviceBusMessage.ApplicationProperties != null)
            {
                var headerAdapter = new ServiceBusHeadersCollectionAdapter(serviceBusMessage.ApplicationProperties);
                var extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headerAdapter);
                var parentContext = extractedContext.SpanContext;

                if (parentContext != null)
                {
                    Log.Information(
                        "Successfully extracted parent context - TraceId: {TraceId}, SpanId: {SpanId}",
                        parentContext.TraceId128,
                        parentContext.SpanId);
                }

                return parentContext;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting parent context from ServiceBus message");
        }

        return null;
    }

    private static Scope? CreateAndConfigureSpan(
        Tracer tracer,
        SpanContext? parentContext,
        IServiceBusReceiver receiverInstance,
        DateTimeOffset startTime,
        Exception? exception)
    {
        var scope = tracer.StartActiveInternal(OperationName, parent: parentContext, startTime: startTime);
        var span = scope.Span;

        try
        {
            span.Type = SpanTypes.Queue;
            span.SetTag(Tags.SpanKind, SpanKinds.Consumer);

            var entityPath = receiverInstance.EntityPath ?? "unknown";
            span.ResourceName = entityPath;

            span.SetTag(Tags.MessagingDestinationName, entityPath);
            span.SetTag(Tags.MessagingOperation, "receive");
            span.SetTag(Tags.MessagingSystem, "servicebus");

            if (exception != null)
            {
                span.SetException(exception);
            }
        }
        finally
        {
            scope.Dispose();
        }

        return scope;
    }

    private static void ReinjectContextIntoMessages(Tracer tracer, Scope scope, System.Collections.IList messagesList)
    {
        try
        {
            var context = new Propagators.PropagationContext(scope.Span.Context, Baggage.Current);

            foreach (var message in messagesList)
            {
                if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true &&
                    serviceBusMessage.ApplicationProperties != null)
                {
                    var headerAdapter = new ServiceBusHeadersCollectionAdapter(serviceBusMessage.ApplicationProperties);
                    tracer.TracerManager.SpanContextPropagator.Inject(context, headerAdapter);

                    Log.Information(
                        "Re-injected context into message - TraceId: {TraceId}, SpanId: {SpanId}",
                        scope.Span.Context.TraceId128,
                        scope.Span.Context.SpanId);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error re-injecting context into ServiceBus messages");
        }
    }

    private readonly struct ReceiveMessagesState
    {
        public readonly IServiceBusReceiver Instance;
        public readonly DateTimeOffset StartTime;

        public ReceiveMessagesState(IServiceBusReceiver instance, DateTimeOffset startTime)
        {
            Instance = instance;
            StartTime = startTime;
        }
    }
}
