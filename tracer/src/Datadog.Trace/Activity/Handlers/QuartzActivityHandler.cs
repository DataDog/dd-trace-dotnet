// <copyright file="QuartzActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Linq;
using Datadog.Trace.Activity.DuckTypes;
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
            => sourceName.StartsWith("Quartz");

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            var tags = new OpenTelemetryTags();

            // Add span.kind tag if not present
            if (activity.Tags.All(tag => tag.Key != "span.kind"))
            {
                // Determine appropriate span kind based on activity operation name or source
                var spanKind = DetermineSpanKind();
                tags.SetTag("span.kind", spanKind);
            }

            ActivityHandlerCommon.ActivityStarted(sourceName, activity, tags: tags, out var activityMapping);

            // Update the span's resource name with job.name tag if available
            if (activityMapping.Scope.Span is Span span)
            {
                UpdateSpanResourceName(span, activity);
            }
        }

        private static string DetermineSpanKind()
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
                    var currentResourceName = span.ResourceName;
                    var newResourceName = $"{currentResourceName} - {jobNameTag.Value}";
                    span.ResourceName = newResourceName;
                }
            }
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            ActivityHandlerCommon.ActivityStopped(sourceName, activity);
        }
    }
}
