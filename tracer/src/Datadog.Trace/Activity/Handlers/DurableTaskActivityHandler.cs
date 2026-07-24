// <copyright file="DurableTaskActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// Bridges host-side Durable Functions distributed tracing activities emitted by the
    /// <c>WebJobs.Extensions.DurableTask</c> ActivitySource into Datadog spans.
    /// </summary>
    internal sealed class DurableTaskActivityHandler : IActivityHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DurableTaskActivityHandler>();

        public bool ShouldListenTo(string sourceName, string? version)
            => sourceName == DurableTaskConstants.WebJobsActivitySourceName
            || sourceName == DurableTaskConstants.SdkActivitySourceName;

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            var tags = new OpenTelemetryTags
            {
                OtelLibraryName = sourceName,
            };

            ActivityHandlerCommon.ActivityStarted(sourceName, activity, tags: tags, out _);
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            ActivityKey key = activity switch
            {
                IW3CActivity { TraceId: not null, SpanId: not null } w3cActivity => new(w3cActivity.TraceId, w3cActivity.SpanId),
                _ => new(activity.Id)
            };

            if (key.IsValid()
             && ActivityHandlerCommon.ActivityMappingById.TryRemove(key, out var activityMapping)
             && activityMapping.Scope.Span is Span span)
            {
                DurableTaskActivityHandlerCommon.EnhanceSpan(span, activity);
                OtlpHelpers.UpdateSpanFromActivity(activity, span, Tracer.Instance.Settings.OtelSemanticsEnabled);
                span.Finish(activity.StartTimeUtc.Add(activity.Duration));
                activityMapping.Scope.Close();
                return;
            }

            Log.Debug(
                "Could not find span for Durable Task activity '{ActivityId}' with key '{Key}', falling back to common handler",
                activity.Id,
                key);

            ActivityHandlerCommon.ActivityStopped(sourceName, activity);
        }
    }
}
