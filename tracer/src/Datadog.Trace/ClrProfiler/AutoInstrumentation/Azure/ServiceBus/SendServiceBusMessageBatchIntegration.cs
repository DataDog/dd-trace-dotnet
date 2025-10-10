// <copyright file="SendServiceBusMessageBatchIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Azure.Messaging.ServiceBus.ServiceBusMessageBatch calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ServiceBusMessageBatch",
        MethodName = "TryAddMessage",
        ReturnTypeName = ClrNames.Bool,
        ParameterTypeNames = new[] { "Azure.Messaging.ServiceBus.ServiceBusMessage" },
        MinimumVersion = "7.14.0",
        MaximumVersion = "7.*.*",
        IntegrationName = nameof(IntegrationId.AzureServiceBus))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SendServiceBusMessageBatchIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SendServiceBusMessageBatchIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMessage">Type of the message</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="message">The message instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message)
            where TTarget : IServiceBusMessageBatch, IDuckType
            where TMessage : IServiceBusMessage
        {
            Scope? messageScope = null;

            var tracer = Tracer.Instance;
            if (tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus)
             && tracer.TracerManager.DataStreamsManager.IsEnabled)
            {
                // Adding DSM to the send operation of ServiceBusMessageBatch - Step One:
                // While we have access to the message object itself, create a mapping from the
                // message application properties dictionary to the message object itself
                AzureServiceBusCommon.SetMessage(message.ApplicationProperties, message.Instance);
            }

            // Create TryAdd message spans for batch with links when enabled
            if (tracer.Settings.AzureServiceBusBatchLinksEnabled)
            {
                messageScope = CreateAddMessageSpan(instance, message);
            }

            return new CallTargetState(messageScope);
        }

        internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(TTarget instance, bool returnValue, Exception? exception, in CallTargetState state)
        {
            if (state.Scope != null)
            {
                if (returnValue && instance != null)
                {
                    BatchSpanContextStorage.AddSpanContext(instance, state.Scope.Span.Context);
                }

                state.Scope.DisposeWithException(exception);
            }

            return new CallTargetReturn<bool>(returnValue);
        }

        private static Scope? CreateAddMessageSpan(IServiceBusMessageBatch batch, IServiceBusMessage message)
        {
            var messageEnumerable = new[] { message };
            var state = AzureServiceBusCommon.CreateSenderSpan(
                batch.ClientDiagnostics,
                operationName: "create",
                messages: messageEnumerable,
                messageCount: 1);

            return state.Scope;
        }
    }
}
