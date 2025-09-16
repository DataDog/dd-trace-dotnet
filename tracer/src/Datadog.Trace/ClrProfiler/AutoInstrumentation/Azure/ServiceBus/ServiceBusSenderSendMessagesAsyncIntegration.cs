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
    private const string DefaultServiceBusPort = "5671";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusSenderSendMessagesAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable messages, ref CancellationToken cancellationToken)
        where TTarget : IServiceBusSender, IDuckType
    {
        var tracer = Tracer.Instance;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus, false))
        {
            return new CallTargetState(null);
        }

        var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureServiceBusTags(SpanKinds.Producer);

        var entityPath = instance.EntityPath ?? "unknown";
        tags.MessagingDestinationName = entityPath;
        tags.MessagingOperation = "send";
        tags.MessagingSystem = "servicebus";
        tags.InstrumentationName = "AzureServiceBus";

        string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName("azureservicebus");
        var scope = tracer.StartActiveInternal(
            OperationName,
            tags: tags,
            serviceName: serviceName);
        var span = scope.Span;

        span.Type = SpanTypes.Queue;
        span.ResourceName = entityPath;

        var endpoint = instance.Connection?.ServiceEndpoint;
        if (endpoint != null)
        {
            tags.NetworkDestinationName = endpoint.Host;
            // https://learn.microsoft.com/en-us/dotnet/api/system.uri.port?view=net-8.0#remarks
            tags.NetworkDestinationPort = endpoint.Port is -1 or 5671 ?
                                DefaultServiceBusPort :
                                endpoint.Port.ToString();
        }

        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
