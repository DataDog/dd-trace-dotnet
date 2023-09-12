// <copyright file="ProcessMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Azure.Messaging.ServiceBus.ReceiverManager.ProcessOneMessage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ReceiverManager",
        MethodName = "ProcessOneMessage",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Azure.Messaging.ServiceBus.ServiceBusReceivedMessage", ClrNames.CancellationToken },
        MinimumVersion = "7.14.0",
        MaximumVersion = "7.*.*",
        IntegrationName = nameof(IntegrationId.AzureServiceBus))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ProcessMessageIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessMessageIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMessage">Type of the message argument</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="message">Instance of the message</param>
        /// <param name="cancellationToken">CancellationToken instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message, CancellationToken cancellationToken)
            where TTarget : IReceiverManager
            where TMessage : IServiceBusReceivedMessage
        {
            // Do not create a span, this will automatically be created by the Azure.Messaging.ServiceBus ActivitySource(s)
            // when the following requirements are met:
            // - AzureServiceBus integration enabled
            // - DD_TRACE_OTEL_ENABLED=true
            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus)
                && Tracer.Instance.InternalActiveScope?.Span is var span)
            {
                PathwayContext? pathwayContext = null;

                var tracer = Tracer.Instance;
                var dataStreamsManager = tracer.TracerManager.DataStreamsManager;

                if (dataStreamsManager.IsEnabled)
                {
                    if (message.ApplicationProperties is not null)
                    {
                        var headers = new ServiceBusHeadersCollectionAdapter(message.ApplicationProperties);

                        try
                        {
                            pathwayContext = dataStreamsManager.ExtractPathwayContextAsBase64String(headers);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error extracting PathwayContext from Azure Service Bus message");
                        }
                    }

                    span.Context.MergePathwayContext(pathwayContext);

                    var consumeTime = span.StartTime.UtcDateTime;
                    var produceTime = message.EnqueuedTime.UtcDateTime;
                    var messageQueueTimeMs = Math.Max(0, (consumeTime - produceTime).TotalMilliseconds);
                    span.Tags.SetMetric(Trace.Metrics.MessageQueueTimeMs, messageQueueTimeMs);

                    var namespaceString = instance.Processor.EntityPath;

                    // TODO: we could pool these arrays to reduce allocations
                    // NOTE: the tags must be sorted in alphabetical order
                    var edgeTags = string.IsNullOrEmpty(namespaceString)
                                        ? new[] { "direction:in", "type:azureservicebus" }
                                        : new[] { "direction:in", $"topic:{namespaceString}", "type:azureservicebus" };

                    span.SetDataStreamsCheckpoint(
                        dataStreamsManager,
                        CheckpointKind.Consume,
                        edgeTags,
                        AzureServiceBusCommon.GetMessageSize(message),
                        (long)messageQueueTimeMs);
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
