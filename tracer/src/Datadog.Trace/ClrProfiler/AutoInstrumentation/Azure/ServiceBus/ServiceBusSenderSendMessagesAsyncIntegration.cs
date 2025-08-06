// <copyright file="ServiceBusSenderSendMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;

/// <summary>
/// System.Threading.Tasks.Task Azure.Messaging.ServiceBus.ServiceBusSender::SendMessagesAsync(System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage],System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
    MethodName = "SendMessagesAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage]", ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ServiceBusSenderSendMessagesAsyncIntegration
{
    private const string OperationName = "azure.servicebus.send";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusSenderSendMessagesAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IEnumerable messages, ref CancellationToken cancellationToken)
    {
        Log.Information("SendMessagesAsync running");

        var tracer = Tracer.Instance;
        var scope = tracer.StartActiveInternal(OperationName);
        var span = scope.Span;
        span.SetTag(Tags.SpanKind, SpanKinds.Producer);
        span.SetTag("azure.servicebus.entity_path", "entity_path");
        span.SetTag("azure.servicebus.namespace", "namespace");
        span.SetTag("azure.servicebus.operation", "send_batch");

        Log.Information("Messages is null: {IsNull}", messages is null);

        if (messages is not null)
        {
            foreach (var message in messages)
            {
                Log.Information("Propagating context for message of type: {MessageType}", message?.GetType()?.FullName ?? "null");

                if (message.TryDuckCast<IServiceBusMessage>(out var serviceBusMessage))
                {
                    Log.Information("Duck casting successful for message");
                    if (serviceBusMessage.ApplicationProperties != null)
                    {
                        var context = new PropagationContext(span.Context, Baggage.Current);
                        tracer.TracerManager.SpanContextPropagator.Inject(context, serviceBusMessage.ApplicationProperties, default(ContextPropagation));
                    }
                    else
                    {
                        Log.Warning("ApplicationProperties is null for message");
                    }
                }
                else
                {
                    Log.Information("Could not duck cast message to IServiceBusMessage");
                }
            }
        }

        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Log.Information("SendMessagesAsync ending");

        Scope? scope = state.Scope;

        if (scope is null)
        {
            Log.Information("Scope is null");
            return returnValue;
        }

        try
        {
            if (exception != null)
            {
                scope.Span.SetException(exception);
            }
        }
        finally
        {
            scope.Dispose();
        }

        return returnValue;
    }
}
