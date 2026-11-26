// <copyright file="QuartzActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Serilog.Events;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz.QuartzCommon;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// Handles Quartz.NET activities for job scheduling and execution.
    /// This handler processes Quartz v4 activities by source name and Quartz v3 activities by operation name.
    /// For Quartz v3, <see cref="DiagnosticListeners.QuartzDiagnosticObserver"/> also sets the activity kind
    /// and records exceptions from the diagnostic events.
    /// </summary>
    internal sealed class QuartzActivityHandler : IActivityHandlerWithOperationName
    {
        private const string ExecuteOperationName = "Quartz.Job.Execute";
        private const string VetoOperationName = "Quartz.Job.Veto";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<QuartzActivityHandler>();
        private static readonly DefaultActivityHandler DefaultHandler = new();

        public bool ShouldListenTo(string sourceName, string? version)
        {
            // Listen to Quartz diagnostic source
            return sourceName.StartsWithOrdinal("Quartz");
        }

        public bool ShouldListenToOperationName(string operationName)
            => operationName is ExecuteOperationName or VetoOperationName;

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.Quartz))
            {
                DefaultHandler.ActivityStarted(sourceName, activity);
                return;
            }

            ActivityHandlerCommon.ActivityStarted(IntegrationId.Quartz, sourceName, activity, tags: new OpenTelemetryTags(), out _);
        }

        public void ActivityStopped<T>(string sourceName, T activity, in ActivityStatus status)
            where T : IActivity5
        {
            if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.Quartz))
            {
                DefaultHandler.ActivityStopped(sourceName, activity, in status);
                return;
            }

            if (TryTakeQuartzScope(activity, out var scope))
            {
                // Finish the span manually
                EnhanceActivityMetadata(activity);

                OtlpHelpers.UpdateSpanFromActivity(activity, scope.Span, in status, scope.Span.OpenTelemetrySemanticsEnabled);
                ActivityHandlerCommon.FinishActivityScope(activity, scope);
            }
            else
            {
                // Fallback to common handler if we couldn't find the span
                ActivityHandlerCommon.ActivityStopped(sourceName, activity, in status);
            }
        }

        public void LegacyActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.Quartz))
            {
                DefaultHandler.LegacyActivityStopped(sourceName, activity);
                return;
            }

            if (TryTakeQuartzScope(activity, out var scope))
            {
                // Finish the span manually
                EnhanceActivityMetadata(activity);

                OtlpHelpers.UpdateSpanFromLegacyActivity(activity, scope.Span, scope.Span.OpenTelemetrySemanticsEnabled);
                ActivityHandlerCommon.FinishActivityScope(activity, scope);
            }
            else
            {
                // Fallback to common handler if we couldn't find the span
                ActivityHandlerCommon.LegacyActivityStopped(sourceName, activity);
            }
        }

        // Find the scope and remove the mapping before the common handler processes it
        private static bool TryTakeQuartzScope<T>(T activity, [NotNullWhen(true)] out Scope? scope)
            where T : IActivity
        {
            var key = ActivityKey.From(activity);

            if (key.IsValid() && ActivityHandlerCommon.ActivityMappingById.TryRemove(key, out var activityMapping) && activityMapping.Scope is { Span: not null } foundScope)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("ActivityStopped: Processing span for activity '{ActivityId}'", activity.Id);
                }

                scope = foundScope;
                return true;
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Could not find span for activity '{ActivityId}' with key '{Key}'", activity.Id, key);
            }

            scope = null;
            return false;
        }
    }
}
