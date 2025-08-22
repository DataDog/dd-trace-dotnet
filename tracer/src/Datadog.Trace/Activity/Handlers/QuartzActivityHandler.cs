// <copyright file="QuartzActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using Datadog.Trace;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

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
            => ActivityHandlerCommon.ActivityStarted(sourceName, activity, tags: new OpenTelemetryTags(), out _);

        private static string DetermineSpanKind<T>(T activity)
            where T : IActivity
        {
            // Default to internal for Quartz operations until we learn there are other span kinds
            return "internal";
        }

        private static void UpdateSpanResourceName<T>(Span span, T activity)
            where T : IActivity
        {
            // Look for job.name tag in the activity tags
            if (activity.Tags != null)
            {
                var jobNameTag = activity.Tags.FirstOrDefault(tag => tag.Key == "job.name");
                if (!string.IsNullOrEmpty(jobNameTag.Value))
                {
                    // Update the span's resource name by appending the job name
                    var originalResourceName = span.ResourceName;
                    var newResourceName = $"{span.OperationName} {jobNameTag.Value}";
                    span.ResourceName = newResourceName;
                    Log.Debug("Updated span resource name from '{OriginalResourceName}' to '{NewResourceName}' for job '{JobName}'", originalResourceName, newResourceName, jobNameTag.Value);
                }
                else
                {
                    Log.Debug("No job.name tag found in activity tags");
                }
            }
            else
            {
                Log.Debug("Activity tags are null");
            }
        }

        private static void UpdateSpanKind<T>(Span span, T activity)
            where T : IActivity
        {
            // Add span.kind tag if not present
            if (activity.Tags != null && !activity.Tags.Any(tag => tag.Key == "span.kind"))
            {
                // Determine appropriate span kind based on activity operation name or source
                string spanKind = DetermineSpanKind(activity);
                span.SetTag("span.kind", spanKind);
            }
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            // Update the span's resource name and span.kind tag if available
            string key;
            if (activity is IW3CActivity w3cActivity)
            {
                key = w3cActivity.TraceId + w3cActivity.SpanId;
            }
            else
            {
                key = activity.Id;
            }

            if (key != null && ActivityHandlerCommon.ActivityMappingById.TryRemove(key, out var activityMapping) && activityMapping.Scope?.Span is Span span)
            {
                Log.Debug("Found span for activity '{ActivityId}', updating resource name and span kind", activity.Id);
                UpdateSpanResourceName(span, activity);
                UpdateSpanKind(span, activity);
                // Finish the span manually since we removed it from the mapping
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
