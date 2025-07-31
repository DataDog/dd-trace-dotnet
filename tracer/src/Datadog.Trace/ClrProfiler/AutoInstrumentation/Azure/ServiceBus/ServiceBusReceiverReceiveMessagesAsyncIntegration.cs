// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// ReceiveMessagesAsyncIntegration class
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ServiceBusReceiver",
        MethodName = "ReceiveMessagesAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]]",
        ParameterTypeNames = new[] { ClrNames.Int32, "System.Nullable`1[System.TimeSpan]", ClrNames.CancellationToken },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ServiceBusReceiverReceiveMessagesAsyncIntegration
    {
        internal const string IntegrationName = nameof(IntegrationId.AzureServiceBus);
        private const string OperationName = "azure.servicebus.receive";

        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int maxMessages, TimeSpan? maxWaitTime, CancellationToken cancellationToken)
        {
            var tracer = Tracer.Instance;
            if (tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
            {
                var scope = tracer.StartActiveInternal(OperationName);
                var span = scope.Span;

                span.SetTag(Tags.SpanKind, SpanKinds.Client);
                span.SetTag("azure.servicebus.entity_path", "entity_path");
                span.SetTag("azure.servicebus.namespace", "namespace");
                span.SetTag("azure.servicebus.operation", "receive_batch");
                span.SetTag("azure.servicebus.receive_mode", "receive_mode");
                span.SetTag("azure.servicebus.max_messages", "1");

                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            var scope = state.Scope;
            if (scope != null)
            {
                if (exception != null)
                {
                    scope.Span.SetException(exception);
                }

                scope.Dispose();
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
