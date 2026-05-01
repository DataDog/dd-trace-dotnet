// <copyright file="SendServiceBusMessagesIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Azure.Messaging.ServiceBus.ServiceBusSender::CreateDiagnosticScope calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ServiceBusSender",
        MethodName = "CreateDiagnosticScope",
        ReturnTypeName = "Azure.Core.Pipeline.DiagnosticScope",
        ParameterTypeNames = new[] { "System.Collections.Generic.IReadOnlyCollection`1[Azure.Messaging.ServiceBus.ServiceBusMessage]", ClrNames.String, "Azure.Core.Shared.MessagingDiagnosticOperation" },
        MinimumVersion = "7.14.0",
        MaximumVersion = "7.*.*",
        IntegrationName = nameof(IntegrationId.AzureServiceBus))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SendServiceBusMessagesIntegration
    {
        private static readonly string[] DefaultProduceEdgeTags = ["direction:out", "type:servicebus"];

        internal static CallTargetState OnMethodBegin<TTarget, TOperation>(TTarget instance, IEnumerable messages, string activityName, TOperation operation)
            where TTarget : IServiceBusSender, IDuckType
        {
            var tracer = Tracer.Instance;
            var dataStreamsManager = tracer.TracerManager.DataStreamsManager;

            if (tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus)
                && dataStreamsManager.IsEnabled
                && tracer.InternalActiveScope?.Span is Span span
                && messages is not null)
            {
                var entityPath = instance.EntityPath;
                var edgeTags = string.IsNullOrEmpty(entityPath)
                    ? DefaultProduceEdgeTags
                    : dataStreamsManager.GetOrCreateEdgeTags(
                        new ServiceBusEdgeTagCacheKey(entityPath!, IsConsume: false),
                        static k => ["direction:out", $"topic:{k.EntityPath}", "type:servicebus"]);
                span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, 0, 0);
                foreach (var msgObj in messages)
                {
                    if (msgObj?.DuckCast<IServiceBusMessage>() is { ApplicationProperties: IDictionary<string, object> props }
                        && !props.ContainsKey(DataStreamsPropagationHeaders.PropagationKeyBase64))
                    {
                        dataStreamsManager.InjectPathwayContextAsBase64String(
                            span.Context.PathwayContext,
                            new AzureHeadersCollectionAdapter(props));
                    }
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
