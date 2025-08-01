// <copyright file="ServiceBusSenderSendMessageAsyncIntegration.cs" company="Datadog">
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
    /// SendMessageAsyncIntegration class
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
        MethodName = "SendMessageAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Azure.Messaging.ServiceBus.ServiceBusMessage", ClrNames.CancellationToken },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ServiceBusSenderSendMessageAsyncIntegration
    {
        internal const string IntegrationName = nameof(IntegrationId.AzureServiceBus);
        private const string OperationName = "azure.servicebus.send";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusSenderSendMessageAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message, CancellationToken cancellationToken)
        {
            Log.Information("SendMessageAsync running");

            var tracer = Tracer.Instance;
            var scope = tracer.StartActiveInternal(OperationName);
            var span = scope.Span;
            span.SetTag(Tags.SpanKind, SpanKinds.Producer);
            span.SetTag("azure.servicebus.entity_path", "entity_path");
            span.SetTag("azure.servicebus.namespace", "namespace");
            span.SetTag("azure.servicebus.operation", "send");
            return new CallTargetState(scope);
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (scope is null)
            {
                Log.Information("Scope is null");
                return returnValue;
            }

            try
            {
                if (responseMessage.Instance is not null)
                {
                    scope.Span.SetHttpStatusCode(responseMessage.StatusCode, false, Tracer.Instance.Settings);
                }

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
