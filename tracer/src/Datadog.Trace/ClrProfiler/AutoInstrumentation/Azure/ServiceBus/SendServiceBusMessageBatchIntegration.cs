// <copyright file="SendServiceBusMessageBatchIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.ComponentModel;
using System.Threading;
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

            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
            {
                if (Tracer.Instance.TracerManager.DataStreamsManager.IsEnabled)
                {
                    // Adding DSM to the send operation of ServiceBusMessageBatch - Step One:
                    // While we have access to the message object itself, create a mapping from the
                    // message application properties dictionary to the message object itself
                    AzureServiceBusCommon.SetMessage(message.ApplicationProperties, message.Instance);
                }

                // Create individual message spans for batch linking when enabled
                if (Tracer.Instance.Settings.AzureServiceBusBatchLinksEnabled)
                {
                    messageScope = CreateMessageSpan(instance, message);
                }
            }

            return new CallTargetState(messageScope);
        }

        internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(TTarget instance, bool returnValue, Exception? exception, in CallTargetState state)
        {
            if (state.Scope != null)
            {
                try
                {
                    // Add the message span context to the batch for later linking
                    if (returnValue && instance != null)
                    {
                        ServiceBusBatchSpanContext.AddMessageSpanContext(instance, state.Scope.Span.Context);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error adding message span context to batch");
                }
                finally
                {
                    state.Scope.DisposeWithException(exception);
                }
            }

            return new CallTargetReturn<bool>(returnValue);
        }

        private static Scope? CreateMessageSpan<TTarget, TMessage>(TTarget batchInstance, TMessage message)
            where TTarget : IServiceBusMessageBatch, IDuckType
            where TMessage : IServiceBusMessage
        {
            try
            {
                var tracer = Tracer.Instance;
                var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureServiceBusTags(SpanKinds.Producer);

                // Get entity path from the batch's client diagnostics
                var entityPath = "unknown";
                if (batchInstance?.ClientDiagnostics?.EntityPath != null)
                {
                    entityPath = batchInstance.ClientDiagnostics.EntityPath;
                }

                tags.MessagingDestinationName = entityPath;
                tags.MessagingOperation = "send";
                tags.MessagingSystem = "servicebus";
                tags.InstrumentationName = "AzureServiceBus";

                string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName("azureservicebus");

                // Extract parent context from message properties (reception span)
                SpanContext? parentContext = null;
                if (message.ApplicationProperties != null)
                {
                    try
                    {
                        var headerAdapter = new ServiceBusHeadersCollectionAdapter(message.ApplicationProperties);
                        var extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headerAdapter);
                        parentContext = extractedContext.SpanContext;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Error extracting parent context from message properties");
                    }
                }

                var scope = tracer.StartActiveInternal(
                    "azure_servicebus.send",
                    parent: parentContext,
                    tags: tags,
                    serviceName: serviceName);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                span.ResourceName = "batch_message";

                // Set message ID if available
                if (!string.IsNullOrEmpty(message.MessageId))
                {
                    span.SetTag(Tags.MessagingMessageId, message.MessageId);
                }

                // Add network tags from batch diagnostics
                if (batchInstance?.ClientDiagnostics?.FullyQualifiedNamespace != null)
                {
                    var fqns = batchInstance.ClientDiagnostics.FullyQualifiedNamespace;
                    // Extract hostname from FQNS (e.g., "myspace.servicebus.windows.net")
                    tags.NetworkDestinationName = fqns;
                    tags.NetworkDestinationPort = "5671"; // Default Service Bus port
                }

                return scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating message span for batch");
                return null;
            }
        }
    }
}
