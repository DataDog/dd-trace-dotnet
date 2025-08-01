// <copyright file="ServiceBusSenderSendMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// SendMessagesAsyncIntegration class
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
        MethodName = "SendMessagesAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "System.Collections.Generic.IEnumerable`1[Azure.Messaging.ServiceBus.ServiceBusMessage]", ClrNames.CancellationToken },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ServiceBusSenderSendMessagesAsyncIntegration
    {
        internal const string IntegrationName = nameof(IntegrationId.AzureServiceBus);
        private const string OperationName = "azure.servicebus.send";

        internal static CallTargetState OnMethodBegin<TTarget, TMessages>(TTarget instance, TMessages messages, CancellationToken cancellationToken)
        {
            var tracer = Tracer.Instance;
            if (tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
            {
                var scope = tracer.StartActiveInternal(OperationName);
                var span = scope.Span;
                span.SetTag(Tags.SpanKind, SpanKinds.Producer);
                span.SetTag("azure.servicebus.entity_path", "entity_path");
                span.SetTag("azure.servicebus.namespace", "namespace");
                span.SetTag("azure.servicebus.operation", "send_batch");
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return returnValue;
        }
    }
}
