// <copyright file="TransportSenderSendAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Azure.Messaging.ServiceBus.Core.TransportSender.SendAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.Core.TransportSender",
        MethodName = "SendAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "System.Collections.Generic.IReadOnlyCollection`1[Azure.Messaging.ServiceBus.ServiceBusMessage]", ClrNames.CancellationToken },
        MinimumVersion = "7.14.0",
        MaximumVersion = "7.*.*",
        IntegrationName = nameof(IntegrationId.AzureServiceBus),
        CallTargetIntegrationKind = CallTargetKind.Derived)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class TransportSenderSendAsyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="messages">Collection instance of messages</param>
        /// <param name="cancellationToken">CancellationToken instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IEnumerable messages, CancellationToken cancellationToken)
            where TTarget : ITransportSender, IDuckType
        {
            // Do not create a span, this will automatically be created by the Azure.Messaging.ServiceBus ActivitySource(s)
            // Requirements:
            // - AzureServiceBus integration enabled
            // - DD_TRACE_OTEL_ENABLED=true
            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus)
                && Tracer.Instance.InternalActiveScope?.Span is var span)
            {
                // TODO: Figure out if we want to overwrite the traceparent/tracestate.
                // There is a span with operation name "Message" whose span context is inserted into the message
                // and the Send span is separate, with a Span Link to the "Message" span.

                // For now, we will NOT overwrite the span context carried by the message.
                // Since the "Message" span has no concept of queue topic/namespace, the DSM pathway context can only be generated at
                // the time of "ServiceBusSender.Send" span.
                // The result is:
                // - Message.Properties[SpanContextPropagation] => "Message" span context
                // - Message.Properties[DSMPropagation] => "ServiceBusSender.Send" pathway context

                if (messages is not null)
                {
                    var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
                    if (dataStreamsManager.IsEnabled)
                    {
                        string namespaceString = instance.EntityPath;

                        var edgeTags = string.IsNullOrEmpty(namespaceString)
                            ? new[] { "direction:out", "type:azureservicebus" }
                            : new[] { "direction:out", $"topic:{namespaceString}", "type:azureservicebus" };
                        span.SetDataStreamsCheckpoint(
                            dataStreamsManager,
                            CheckpointKind.Produce,
                            edgeTags,
                            0, // message.Body.Length?
                            0); // tags.MessageQueueTimeMs
                    }

                    foreach (var message in messages)
                    {
                        if (message.TryDuckCast<ServiceBusMessageStruct>(out var serviceBusMessage))
                        {
                            var adapter = new ServiceBusHeadersCollectionAdapter(serviceBusMessage.ApplicationProperties);

                            // If we decide to overwrite the properties in the Message object,
                            // inject the "ServiceBusSender.Send" span context into the message.
                            //
                            // Fixup: Ensure that the value of the "Diagnostic-Id" matches the value of "traceparent",
                            // since we may have updated "traceparent"
                            /*
                            SpanContextPropagator.Instance.Inject(span.Context, adapter);
                            if (serviceBusMessage.ApplicationProperties.TryGetValue("traceparent", out object value)
                                && value is string traceparent)
                            {
                                serviceBusMessage.ApplicationProperties["Diagnostic-Id"] = traceparent;
                            }
                            */

                            if (dataStreamsManager.IsEnabled)
                            {
                                dataStreamsManager.InjectPathwayContextAsBase64String(span.Context.PathwayContext, adapter);
                            }
                        }
                    }
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
