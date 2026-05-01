// <copyright file="ActivityHandlerCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Activity.Handlers
{
    internal sealed class ActivityHandlerCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityHandlerCommon));
        internal static readonly ConcurrentDictionary<ActivityKey, ActivityMapping> ActivityMappingById = new();
        private static readonly IntegrationId IntegrationId = IntegrationId.OpenTelemetry;

        // Periodic sweep that handles two reliability cases that the hot-path Stop callback
        // cannot: 1. Activities the customer abandoned without calling Stop (their WeakReference
        // target is GC'd); 2. Activities whose Stop fired but our listener callback missed.
        private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromSeconds(10);
        private static int _sweepInProgress;
        private static Timer? _reconciliationTimer;

        /// <summary>
        /// Handles when a new Activity is started to map it to a new <see cref="Span"/>/<see cref="Scope"/>.
        /// </summary>
        /// <param name="sourceName">The name of the Activity source</param>
        /// <param name="activity">The Activity object</param>
        /// <param name="tags">
        /// The tags that will be associated with the <see cref="Span"/>.
        /// <see cref="OpenTelemetryTags"/> is used for mapping the operation name.
        /// </param>
        /// <param name="activityMapping">The mapping of Activity to its <see cref="Scope"/>.</param>
        /// <typeparam name="T">The <see cref="IActivity"/>.</typeparam>
        public static void ActivityStarted<T>(string sourceName, T activity, OpenTelemetryTags? tags, out ActivityMapping activityMapping)
            where T : IActivity
        {
            Tracer.Instance.TracerManager.Telemetry.IntegrationRunning(IntegrationId);

            // Propagate Trace and Parent Span ids
            SpanContext? parent = null;
            TraceId traceId = default;
            ulong spanId = 0;
            string? rawTraceId = null;
            string? rawSpanId = null;

            // for non-W3C activities using Hierarchical IDs (both IW3CActivity and IActivity) we use Activity.Id and string.Empty
            // for W3C ID activities (always IW3CActivity) we'll use the Activity.TraceId + Activity.SpanId as the key
            ActivityKey? activityKey = null;

            if (activity is IW3CActivity activity3)
            {
                var activityTraceId = activity3.TraceId;
                var activitySpanId = activity3.SpanId;

                if (!StringUtil.IsNullOrEmpty(activityTraceId))
                {
                    // W3C ID
                    if (activity3 is { RawParentSpanId: { } parentSpanId })
                    {
                        // This will be true for activities using W3C IDs which have a "remote" parent span ID
                        // We explicitly don't check the case where we _do_ have a Parent object (i.e. in-process activity)
                        // as in that scenario we may need to remap the parent instead (see below).
                        //
                        // We know that we have a parent context, but we use TraceId+ParentSpanId for the mapping.
                        // This is a result of an issue with OTel v1.0.1 (unsure if OTel or us tbh) where the
                        // ".ParentId" matched for the Trace+Span IDs but not for the flags portion.
                        // Doing a lookup on just the TraceId+ParentSpanId seems to be more resilient.
                        if (ActivityMappingById.TryGetValue(new ActivityKey(activityTraceId, parentSpanId), out ActivityMapping mapping))
                        {
                            parent = mapping.Scope.Span.Context;
                        }
                        else
                        {
                            // create a new parent span context for the ActivityContext
                            _ = HexString.TryParseTraceId(activityTraceId, out var newActivityTraceId);
                            _ = HexString.TryParseUInt64(parentSpanId, out var newActivitySpanId);

                            parent = Tracer.Instance.CreateSpanContext(
                                SpanContext.None,
                                traceId: newActivityTraceId,
                                spanId: newActivitySpanId,
                                rawTraceId: activityTraceId,
                                rawSpanId: parentSpanId);
                        }
                    }
                }
                else
                {
                    // No traceID, so must be Hierarchical ID
                    if (activity3 is { RawParentSpanId: { } parentSpanId })
                    {
                        // This is a weird scenario - we're in a hierarchical ID, we don't have a trace ID, but we _do_ have a _parentSpanID?!
                        // should never hit this path unless we've gone wrong somewhere
                        Log.Error("Activity with ID {ActivityId} had parent span ID {ParentSpanId} but TraceID was missing", activity.Id, parentSpanId);
                    }
                    else
                    {
                        // Since _parentSpanID is null, this either grabs _parentId, or Parent.Id, depending on what was set
                        var parentId = activity3.ParentId;
                        if (!StringUtil.IsNullOrEmpty(parentId) && ActivityMappingById.TryGetValue(new ActivityKey(parentId), out ActivityMapping mapping))
                        {
                            parent = mapping.Scope.Span.Context;
                        }
                    }
                }

                // If we don't have a remote context, then we may need to remap the current activity to
                // reparent it with a datadog span
                if (parent is null
                 && activitySpanId is not null
                 && activityTraceId is not null
                 && Tracer.Instance.ActiveScope?.Span is Span activeSpan
                 && (activity.Parent is null || activity.Parent.StartTimeUtc <= activeSpan.StartTime.UtcDateTime))
                {
                    // We ensure the activity follows the same TraceId as the span
                    // And marks the ParentId the current spanId
                    // TraceId (always 32 chars long even when using 64-bit ids)
                    activity3.TraceId = activeSpan.Context.RawTraceId;
                    activityTraceId = activity3.TraceId;

                    // SpanId (always 16 chars long)
                    activity3.RawParentSpanId = activeSpan.Context.RawSpanId;

                    // We clear internal IDs to force recalculation.
                    activity3.RawId = null;
                    activity3.RawParentId = null;

                    // Avoid recalculation of the traceId.
                    traceId = activeSpan.TraceId128;
                }

                // if there's an existing Activity we try to use its TraceId and SpanId,
                // but if Activity.IdFormat is not ActivityIdFormat.W3C, they may be null or unparsable
                if (activityTraceId != null && activitySpanId != null)
                {
                    if (traceId == TraceId.Zero)
                    {
                        _ = HexString.TryParseTraceId(activityTraceId, out traceId);
                    }

                    _ = HexString.TryParseUInt64(activitySpanId, out spanId);

                    rawTraceId = activityTraceId;
                    rawSpanId = activitySpanId;
                    activityKey = new(traceId: activityTraceId, spanId: activitySpanId);
                }
            }
            else
            {
                // non-IW3CActivity, i.e. we're in .NET Core 2.x territory. Only have hierarchical IDs to worry about here
                var parentId = activity.ParentId;
                if (!StringUtil.IsNullOrEmpty(parentId) && ActivityMappingById.TryGetValue(new ActivityKey(parentId), out ActivityMapping mapping))
                {
                    parent = mapping.Scope.Span.Context;
                }
            }

            try
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(
                        "DefaultActivityHandler.ActivityStarted: [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]",
                        new object[]
                        {
                            sourceName,
                            activity.Id,
                            activity.RootId,
                            activity.OperationName!,
                            activity.StartTimeUtc,
                            activity.Duration
                        });
                }

                // We check if we have to ignore the activity by the operation name value
                if (IgnoreActivityHandler.ShouldIgnoreByOperationName(activity.OperationName))
                {
                    IgnoreActivityHandler.IgnoreActivity(activity, Tracer.Instance.ActiveScope?.Span as Span);
                    activityMapping = default;
                    return;
                }

                activityKey ??= new(activity.Id);

                if (!activityKey.Value.IsValid())
                {
                    // identified by Error Tracking
                    // unsure how exactly this occurs after reading through the Activity source code
                    // Activity.Id, Activity.SpanId and/or Activity.TraceId were null
                    // if this is the case, just ignore the Activity
                    activityMapping = default;
                    return;
                }

#if NETCOREAPP
                // Avoid closure allocation if we can
                activityMapping = ActivityMappingById.GetOrAdd(
                    activityKey.Value,
                    static (_, details) => new(new(details.activity.Instance!), CreateScopeFromActivity(details.activity, details.tags, details.parent, details.traceId, details.spanId, details.rawTraceId, details.rawSpanId)),
                    (activity, tags, parent, traceId, spanId, rawTraceId, rawSpanId));
#else
                activityMapping = ActivityMappingById.GetOrAdd(activityKey.Value, _ => new(new(activity.Instance!), CreateScopeFromActivity(activity, tags, parent, traceId, spanId, rawTraceId, rawSpanId)));
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStarted callback");
                activityMapping = default;
            }

            static Scope CreateScopeFromActivity(T activity, OpenTelemetryTags? tags, SpanContext? parent, TraceId traceId, ulong spanId, string? rawTraceId, string? rawSpanId)
            {
                var span = Tracer.Instance.StartSpan(
                    activity.OperationName,
                    tags: tags ?? new OpenTelemetryTags(),
                    parent: parent,
                    startTime: activity.StartTimeUtc,
                    traceId: traceId,
                    spanId: spanId,
                    rawTraceId: rawTraceId,
                    rawSpanId: rawSpanId);

                Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
                return Tracer.Instance.ActivateSpan(span, finishOnClose: false);
            }
        }

        public static void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            try
            {
                if (activity.Instance is null)
                {
                    Log.Information("DefaultActivityHandler.ActivityStopped: [Missing Activity]");
                    return;
                }

                if (IgnoreActivityHandler.ShouldIgnoreByOperationName(activity.OperationName))
                {
                    return;
                }

                // Non-w3c activities will have null trace/span IDs, even though they implement IW3CActivity
                ActivityKey key;
                if (activity is IW3CActivity w3cActivity
                 && w3cActivity.TraceId != null!
                 && w3cActivity.SpanId != null!)
                {
                    key = new(w3cActivity.TraceId, w3cActivity.SpanId);
                }
                else
                {
                    key = new(activity.Id);
                }

                if (!key.IsValid())
                {
                    // Adding this as a protective measure as Error Tracking identified
                    // instances where StartActivity had an Activity with null Id, SpanId, TraceId
                    // In that case we just skip the Activity, so doing the same thing here.
                    return;
                }

                if (ActivityMappingById.TryRemove(key, out ActivityMapping someValue) && someValue.Scope?.Span is not null)
                {
                    // We have the exact scope associated with the Activity
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("DefaultActivityHandler.ActivityStopped: [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]", new object[] { sourceName, activity.Id, activity.RootId, activity.OperationName!, activity.StartTimeUtc, activity.Duration });
                    }

                    CloseActivityScope(activity, someValue.Scope);
                    return;
                }

                // No matching entry — either Start was never observed, or the periodic
                // reconciliation sweep already cleaned this one up. Nothing to do here.
                if (Log.IsEnabled(LogEventLevel.Information))
                {
                    Log.Information("DefaultActivityHandler.ActivityStopped: MISSING SCOPE [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]", new object[] { sourceName, activity.Id, activity.RootId, activity.OperationName!, activity.StartTimeUtc, activity.Duration });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the DefaultActivityHandler.ActivityStopped callback");
            }
        }

        internal static void StartReconciliationLoop()
        {
            if (Volatile.Read(ref _reconciliationTimer) is not null)
            {
                return;
            }

            var timer = new Timer(static _ => RunReconciliationSweep(), state: null, dueTime: ReconciliationInterval, period: ReconciliationInterval);
            if (Interlocked.CompareExchange(ref _reconciliationTimer, timer, null) is not null)
            {
                // Another thread won the race and already started the loop
                timer.Dispose();
            }
        }

        internal static void StopReconciliationLoop()
        {
            var timer = Interlocked.Exchange(ref _reconciliationTimer, null);
            timer?.Dispose();
        }

        internal static void RunReconciliationSweep()
        {
            // Skip if a sweep is already in flight (Timer callbacks can overlap on the threadpool).
            if (Interlocked.CompareExchange(ref _sweepInProgress, 1, 0) == 1)
            {
                return;
            }

            try
            {
                var result = ReconcileSweepCore(ActivityMappingById);
                if ((result.GcCollected > 0 || result.MissedStop > 0) && Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug<int, int, int>(
                        "ActivityHandlerCommon: reconciliation swept {GcCollected} GC'd activities and {MissedStop} missed-Stop activities (iterated {Iterated})",
                        result.GcCollected,
                        result.MissedStop,
                        result.Iterated);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Activity reconciliation sweep");
            }
            finally
            {
                Interlocked.Exchange(ref _sweepInProgress, 0);
            }
        }

        [TestingAndPrivateOnly]
        internal static ReconciliationSweepResult ReconcileSweepCore(ConcurrentDictionary<ActivityKey, ActivityMapping> mappings)
        {
            var gcCollected = 0;
            var missedStop = 0;
            var iterated = 0;

            foreach (var kvp in mappings)
            {
                iterated++;
                var activityRef = kvp.Value.Activity;

                if (!activityRef.TryGetTarget(out var activityObject))
                {
                    // Activity was GC'd before Stop was called. Take ownership atomically; if
                    // another thread already removed this entry, bail without closing.
                    if (mappings.TryRemove(kvp.Key, out var owned) && owned.Scope is { Span: { } span } scope)
                    {
                        // Do some "clean up" of the span, to make sure it has sensible defaults
                        // Roughly analogous to OtlpHelpers.AgentConvertSpan, but much more basic with extra assumption
                        if (span.Tags is OpenTelemetryTags tags)
                        {
                            if (string.IsNullOrEmpty(tags.SpanKind))
                            {
                                tags.SpanKind = SpanKinds.Internal;
                            }

                            if (string.IsNullOrEmpty(span.OperationName))
                            {
                                span.OperationName = OperationNameMapper.GetOperationName(tags);
                            }

                            if (string.IsNullOrEmpty(tags.OtelStatusCode))
                            {
                                tags.OtelStatusCode = "STATUS_CODE_UNSET";
                            }

                            if (span.ServiceName is null)
                            {
                                // this is _Very_ unlikely to be set, as we won't have copied the tags
                                // across before the Activity was "lost" to GC, but check it just in case
                                span.SetService(
                                    span.GetTag("peer.service") switch
                                    {
                                        { } peerService when !string.IsNullOrEmpty(peerService) => peerService,
                                        _ => "OTLPResourceNoServiceName",
                                    },
                                    source: null);
                            }

                            if (string.IsNullOrEmpty(span.ResourceName))
                            {
                                span.ResourceName = "ONGOING_ACTIVITY";
                            }

                            if (string.IsNullOrWhiteSpace(span.Type))
                            {
                                span.Type = SpanTypes.Custom;
                            }
                        }

                        span.SetTag("closed_reason", "garbage_collected");

                        span.Finish();
                        scope.Close();
                        gcCollected++;
                    }

                    continue;
                }

                // Activity is still alive. A non-zero Duration on an entry that's still in
                // the dictionary means Stop() was called but our listener callback didn't
                // handle it — close it using the real end time.
                if (activityObject.TryDuckCast<IActivity6>(out var activity6))
                {
                    if (activity6.Duration != TimeSpan.Zero
                     && mappings.TryRemove(kvp.Key, out var owned)
                     && owned.Scope is { } scope)
                    {
                        CloseActivityScope(activity6, scope);
                        missedStop++;
                    }
                }
                else if (activityObject.TryDuckCast<IActivity5>(out var activity5))
                {
                    if (activity5.Duration != TimeSpan.Zero
                     && mappings.TryRemove(kvp.Key, out var owned)
                     && owned.Scope is { } scope)
                    {
                        CloseActivityScope(activity5, scope);
                        missedStop++;
                    }
                }
                else if (activityObject.TryDuckCast<IActivity>(out var activity4))
                {
                    if (activity4.Duration != TimeSpan.Zero
                     && mappings.TryRemove(kvp.Key, out var owned)
                     && owned.Scope is { } scope)
                    {
                        CloseActivityScope(activity4, scope);
                        missedStop++;
                    }
                }
            }

            return new ReconciliationSweepResult(gcCollected, missedStop, iterated);
        }

        private static void CloseActivityScope<TInner>(TInner activity, Scope scope)
            where TInner : IActivity
        {
            var span = scope.Span;
            OtlpHelpers.UpdateSpanFromActivity(activity, scope.Span);

            // OpenTelemtry SDK / OTLP Fixups
            // TODO
            // 3) Add resources to spans
            // OpenTelemetry SDK resources are added to the span attributes by the configured exporter when OpenTelemetry.BaseExporter<T>.Export is called (e.g. OpenTelemetry.Exporter.ConsoleActivityExporter.Export)
            // **** Note: The exporter has a ParentProvider field that is populated with the TracerProviderSdk when everything is initially built, so this is technically per instance
            // **** To reliably get this, we might consider addding a Processor to the TracerProviderBuilder, though not sure where to invoke it
            // - service.instance.id
            // - service.name
            // - service.namespace
            // - service.version
            span.Finish(activity.StartTimeUtc.Add(activity.Duration));

            scope.Close();
        }

        internal readonly struct ReconciliationSweepResult
        {
            public readonly int GcCollected;
            public readonly int MissedStop;
            public readonly int Iterated;

            public ReconciliationSweepResult(int gcCollected, int missedStop, int iterated)
            {
                GcCollected = gcCollected;
                MissedStop = missedStop;
                Iterated = iterated;
            }
        }
    }
}
