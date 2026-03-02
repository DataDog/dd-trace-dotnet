// <copyright file="MassTransitActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// Handles MassTransit activities for message bus operations.
    /// This handler captures MassTransit ActivitySource events to trace messaging operations.
    /// This handler is responsible for MassTransit v8.x and later.
    /// Earlier MassTransit library versions (7.x) are handled by:
    /// - tracer/src/Datadog.Trace/DiagnosticListeners/MassTransitDiagnosticObserver.cs
    /// </summary>
    internal sealed class MassTransitActivityHandler : IActivityHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<MassTransitActivityHandler>();

        public bool ShouldListenTo(string sourceName, string? version)
        {
            // Listen to MassTransit ActivitySource
            // MassTransit 8+ uses ActivitySource named "MassTransit"
            return sourceName.Equals("MassTransit", StringComparison.OrdinalIgnoreCase);
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            // Check if MassTransit integration is enabled
            if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.MassTransit))
            {
                Log.Debug("MassTransit integration is disabled, skipping activity");
                return;
            }

            // Start the activity with OpenTelemetry tags
            ActivityHandlerCommon.ActivityStarted(sourceName, activity, tags: new OpenTelemetryTags(), out var activityMapping);

            Log.Debug("MassTransitActivityHandler.ActivityStarted: Activity '{ActivityId}' started", activity.Id);
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            // Find the span and update it before the common handler processes it
            ActivityKey key = activity switch
            {
                IW3CActivity { TraceId: not null, SpanId: not null } w3cActivity => new(w3cActivity.TraceId, w3cActivity.SpanId),
                _ => new(activity.Id)
            };

            if (key.IsValid() && ActivityHandlerCommon.ActivityMappingById.TryRemove(key, out var activityMapping) && activityMapping.Scope.Span is Span span)
            {
                Log.Debug("MassTransitActivityHandler.ActivityStopped: Processing span for activity '{ActivityId}'", activity.Id);

                // Enhance the activity metadata before converting to span
                if (activity is IActivity5 activity5)
                {
                    MassTransitCommon.EnhanceActivityMetadata(activity5);
                    MassTransitCommon.SetActivityKind(activity5);
                }

                // Update the span from the enhanced activity
                OtlpHelpers.UpdateSpanFromActivity(activity, span);

                // Set clean operation name on the span
                // Use the messaging.operation value directly (send, receive, process)
                var messagingOperation = span.GetTag(Tags.MessagingOperation);
                if (!string.IsNullOrEmpty(messagingOperation))
                {
                    span.OperationName = $"masstransit.{messagingOperation}";
                }

                // Set span type to queue
                span.Type = SpanTypes.Queue;

                // Record telemetry for MassTransit integration
                Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.MassTransit);

                // Finish the span
                span.Finish(activity.StartTimeUtc.Add(activity.Duration));
                activityMapping.Scope.Close();

                Log.Debug("MassTransitActivityHandler.ActivityStopped: Span closed for activity '{ActivityId}'", activity.Id);
            }
            else
            {
                Log.Debug("MassTransitActivityHandler: Could not find span for activity '{ActivityId}' with key '{Key}'", activity.Id, key);
                // Fallback to common handler if we couldn't find the span
                ActivityHandlerCommon.ActivityStopped(sourceName, activity);
            }
        }
    }
}
