// <copyright file="ServiceBusSenderSendMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

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
    MinimumVersion = "7.14.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ServiceBusSenderSendMessagesAsyncIntegration
{
    private const string OperationName = "azure_servicebus.send";
    private const string MessagingType = "servicebus";
    private const int DefaultServiceBusPort = 5671;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusSenderSendMessagesAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable messages, ref CancellationToken cancellationToken)
        where TTarget : IServiceBusSender, IDuckType
    {
        var tracer = Tracer.Instance;
        var scope = tracer.StartActiveInternal(OperationName);
        var span = scope.Span;

        span.Type = SpanTypes.Queue;
        span.SetTag(Tags.SpanKind, SpanKinds.Producer);

        var entityPath = instance.EntityPath ?? "unknown";

        span.ResourceName = entityPath;

        span.SetTag(Tags.MessagingDestinationName, entityPath);
        span.SetTag(Tags.MessagingOperation, "send");
        span.SetTag(Tags.MessagingSystem, "servicebus");

        var endpoint = instance.Connection?.ServiceEndpoint;
        if (endpoint != null)
        {
            span.SetTag(Tags.NetworkDestinationName, endpoint.Host);
            // https://learn.microsoft.com/en-us/dotnet/api/system.uri.port?view=net-8.0#remarks
            var port = endpoint.Port == -1 ? DefaultServiceBusPort : endpoint.Port;
            span.SetTag(Tags.NetworkDestinationPort, port.ToString());
        }

        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Scope? scope = state.Scope;

        if (scope is null)
        {
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
