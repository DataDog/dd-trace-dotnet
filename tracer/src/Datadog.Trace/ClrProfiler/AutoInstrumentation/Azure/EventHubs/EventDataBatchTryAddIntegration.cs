// <copyright file="EventDataBatchTryAddIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Azure.Messaging.EventHubs.Producer.EventDataBatch.TryAdd calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.Producer.EventDataBatch",
        MethodName = "TryAdd",
        ReturnTypeName = ClrNames.Bool,
        ParameterTypeNames = new[] { "Azure.Messaging.EventHubs.EventData" },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventDataBatchTryAddIntegration
    {
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventDataBatchTryAddIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TEventData>(
            TTarget instance,
            TEventData eventData)
            where TEventData : IEventData, IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
            {
                return CallTargetState.GetDefault();
            }

            try
            {
                var activeScope = Tracer.Instance.ActiveScope;
                if (activeScope != null && eventData?.Properties != null)
                {
                    // Inject trace context into the event before it's added to the batch
                    AzureMessagingCommon.InjectContext(eventData.Properties, activeScope as Scope);
                    Log.Debug(
                        LogPrefix + "Injected trace context into EventData before adding to batch. MessageId: {0}",
                        eventData.MessageId ?? "(null)");
                }
                else
                {
                    if (activeScope == null)
                    {
                        Log.Debug(LogPrefix + "No active scope to inject into EventData");
                    }
                    else if (eventData?.Properties == null)
                    {
                        Log.Debug(LogPrefix + "EventData.Properties is null, cannot inject trace context");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error injecting trace context into EventData");
            }

            return CallTargetState.GetDefault();
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(
            TTarget instance,
            TReturn returnValue,
            Exception? exception,
            in CallTargetState state)
        {
            // Nothing to do on method end
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
