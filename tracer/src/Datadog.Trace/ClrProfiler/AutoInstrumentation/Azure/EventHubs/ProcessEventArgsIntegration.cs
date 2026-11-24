// <copyright file="ProcessEventArgsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

/// <summary>
/// System.Void Azure.Messaging.EventHubs.Processor.ProcessEventArgs::.ctor(Azure.Messaging.EventHubs.Consumer.PartitionContext,Azure.Messaging.EventHubs.EventData,System.Func`2[System.Threading.CancellationToken,System.Threading.Tasks.Task],System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.EventHubs",
    TypeName = "Azure.Messaging.EventHubs.Processor.ProcessEventArgs",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Azure.Messaging.EventHubs.Consumer.PartitionContext", "Azure.Messaging.EventHubs.EventData", "System.Func`2[System.Threading.CancellationToken,System.Threading.Tasks.Task]", ClrNames.CancellationToken],
    MinimumVersion = "5.9.2",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.AzureEventHubs))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ProcessEventArgsIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessEventArgsIntegration));

    internal static CallTargetState OnMethodBegin<TTarget, TPartition, TEventData, TUpdateCheckpoint>(TTarget instance, TPartition partition, TEventData data, TUpdateCheckpoint updateCheckpointImplementation, CancellationToken cancellationToken)
    {
        if (data is null)
        {
            return CallTargetState.GetDefault();
        }

        try
        {
            var tracer = Tracer.Instance;
            if (tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs)
                && tracer.TracerManager.DataStreamsManager is { IsEnabled: true })
            {
                EventHubsCommon.RestoreConsumePathway(data);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error restoring the consume pathway for an Event Hubs event handed to the processor");
        }

        return CallTargetState.GetDefault();
    }
}
