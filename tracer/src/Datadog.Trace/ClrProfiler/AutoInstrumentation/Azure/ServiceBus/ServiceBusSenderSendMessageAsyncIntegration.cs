// <copyright file="ServiceBusSenderSendMessageAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
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

        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message, ref CancellationToken cancellationToken)
            where TMessage : IServiceBusMessage
        {
            Log.Information("SendMessageAsync running");

            var tracer = Tracer.Instance;
            var scope = tracer.StartActiveInternal(OperationName);
            var span = scope.Span;
            span.SetTag(Tags.SpanKind, SpanKinds.Producer);
            span.SetTag("azure.servicebus.entity_path", "entity_path");
            span.SetTag("azure.servicebus.namespace", "namespace");
            span.SetTag("azure.servicebus.operation", "send");

            if (message.ApplicationProperties != null)
            {
                var context = new PropagationContext(span.Context, Baggage.Current);
                tracer.TracerManager.SpanContextPropagator.Inject(context, message.ApplicationProperties, default(ContextPropagation));
            }
            else
            {
                Log.Warning("ApplicationProperties is null for message");
            }

            return new CallTargetState(scope);
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Log.Information("SendMessageAsync ending");

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
