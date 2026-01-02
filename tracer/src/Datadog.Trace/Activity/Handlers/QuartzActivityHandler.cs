// <copyright file="QuartzActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz.QuartzCommon;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// Handles Quartz.NET activities for job scheduling and execution.
    /// This handler captures Quartz diagnostic events to trace job execution,
    /// scheduling, and other Quartz-related operations.
    /// This handler is responsible for Quartz v4.x.
    /// Earlier Quartz library versions are handled by:
    /// - tracer/src/Datadog.Trace/DiagnosticListeners/QuartzDiagnosticObserver.cs
    /// - tracer/src/Datadog.Trace/Activity/Handlers/DefaultActivityHandler.cs
    /// </summary>
    internal sealed class QuartzActivityHandler : IActivityHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<QuartzActivityHandler>();

        public bool ShouldListenTo(string sourceName, string? version)
        {
            // Listen to Quartz diagnostic source
            return sourceName.StartsWith("Quartz");
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            ActivityHandlerCommon.ActivityStarted(sourceName, activity, tags: new OpenTelemetryTags(), out var activityMapping);
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            // Find the span and update it before the common handler processes it
            ActivityKey key = activity switch
            {
                IW3CActivity w3cActivity => new(w3cActivity.TraceId, w3cActivity.SpanId),
                _ => new(activity.Id)
            };

            if (key.IsValid() && ActivityHandlerCommon.ActivityMappingById.TryRemove(key, out var activityMapping) && activityMapping.Scope.Span is Span span)
            {
                Log.Debug("ActivityStopped: Processing span for activity '{ActivityId}'", activity.Id);

                // Finish the span manually
                // Finish the span manually
                if (activity is IActivity5 activity5)
                {
                    EnhanceActivityMetadata(activity5);
                }

                OtlpHelpers.UpdateSpanFromActivity(activity, span);

                span.Finish(activity.StartTimeUtc.Add(activity.Duration));
                activityMapping.Scope.Close();
            }
            else
            {
                Log.Debug("Could not find span for activity '{ActivityId}' with key '{Key}'", activity.Id, key);
                // Fallback to common handler if we couldn't find the span
                ActivityHandlerCommon.ActivityStopped(sourceName, activity);
            }
        }
    }
}
