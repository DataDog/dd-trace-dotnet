// <copyright file="ServiceBusReceiverReceiveMessageAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// ReceiveMessageAsyncIntegration class
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ServiceBusReceiver",
        MethodName = "ReceiveMessageAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]",
        ParameterTypeNames = new[] { "System.Nullable`1[System.TimeSpan]", ClrNames.CancellationToken },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ServiceBusReceiverReceiveMessageAsyncIntegration
    {
        internal const string IntegrationName = nameof(IntegrationId.AzureServiceBus);
        private const string OperationName = "azure.servicebus.receive";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusReceiverReceiveMessageAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, TimeSpan? maxWaitTime, CancellationToken cancellationToken)
        {
            Log.Information("ReceiveMessageAsync running");

            var tracer = Tracer.Instance;
            var scope = tracer.StartActiveInternal(OperationName);
            var span = scope.Span;
            span.SetTag(Tags.SpanKind, SpanKinds.Consumer);
            span.SetTag("azure.servicebus.entity_path", "entity_path");
            span.SetTag("azure.servicebus.namespace", "namespace");
            span.SetTag("azure.servicebus.operation", "receive");
            span.SetTag("azure.servicebus.receive_mode", "receive_mode");
            return new CallTargetState(scope);
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Scope scope = state.Scope;

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
}
