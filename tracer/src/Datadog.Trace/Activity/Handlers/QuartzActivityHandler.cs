// <copyright file="QuartzActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
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
    /// </summary>
    internal class QuartzActivityHandler : IActivityHandler
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

        private static void UpdateSpanResourceName<T>(Span span, T activity)
            where T : IActivity
        {
            // Look for job.name tag in the activity tags
            if (activity.Tags is not null && activity.OperationName is not null)
            {
                var jobNameTag = activity.Tags.FirstOrDefault(tag => tag.Key == "job.name");
                if (!string.IsNullOrEmpty(jobNameTag.Value))
                {
                    // Update the span's resource name by appending the job name
                    var originalResourceName = span.ResourceName;
                    var newResourceName = CreateResourceName(activity.OperationName, jobNameTag.Value);
                    span.ResourceName = newResourceName;
                    Log.Debug("Updated span resource name from '{OriginalResourceName}' to '{NewResourceName}' for job '{JobName}'", originalResourceName, newResourceName, jobNameTag.Value);
                }
                else
                {
                    Log.Debug("No job.name tag found in Activity.Tags");
                }
            }
            else
            {
                Log.Debug("Activity.Tags or Activity.OperationName are null");
            }
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            // Find the span and update it before the common handler processes it
            string key;
            if (activity is IW3CActivity w3cActivity)
            {
                key = w3cActivity.TraceId + w3cActivity.SpanId;
            }
            else
            {
                key = activity.Id;
            }

            if (key != null && ActivityHandlerCommon.ActivityMappingById.TryRemove(key, out var activityMapping) && activityMapping.Scope.Span is Span span)
            {
                Log.Debug("ActivityStopped: Processing span for activity '{ActivityId}'", activity.Id);

                // Finish the span manually
                OtlpHelpers.UpdateSpanFromActivity(activity, span);
                UpdateSpanResourceName(span, activity);

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
