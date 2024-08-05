// <copyright file="ActivityHandlerCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Activity.Handlers
{
    internal class ActivityHandlerCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityHandlerCommon));
        internal static readonly ConcurrentDictionary<string, ActivityMapping> ActivityMappingById = new();
        private static readonly IntegrationId IntegrationId = IntegrationId.OpenTelemetry;

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
            var activeSpan = Tracer.Instance.ActiveScope?.Span as Span;

            // Propagate Trace and Parent Span ids
            SpanContext? parent = null;
            TraceId traceId = default;
            ulong spanId = 0;
            string? rawTraceId = null;
            string? rawSpanId = null;

            // for non-IW3CActivity interfaces we'll use Activity.Id as the key as they don't have a guaranteed TraceId+SpanId
            // for IW3CActivity interfaces we'll use the Activity.TraceId + Activity.SpanId as the key
            // have to also validate that the TraceId and SpanId actually exist and aren't null - as they can be in some cases
            string? activityKey = null;

            if (activity is IW3CActivity w3cActivity)
            {
                var activityTraceId = w3cActivity.TraceId;
                var activitySpanId = w3cActivity.SpanId;

                // If the user has specified a parent context, get the parent Datadog SpanContext
                if (w3cActivity is { ParentSpanId: { } parentSpanId, ParentId: { } parentId })
                {
                    // We know that we have a parent context, but we use TraceId+ParentSpanId for the mapping.
                    // This is a result of an issue with OTel v1.0.1 (unsure if OTel or us tbh) where the
                    // ".ParentId" matched for the Trace+Span IDs but not for the flags portion.
                    // Doing a lookup on just the TraceId+ParentSpanId seems to be more resilient.
                    if (activityTraceId != null!)
                    {
                        if (ActivityMappingById.TryGetValue(activityTraceId + parentSpanId, out ActivityMapping mapping))
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
                    else
                    {
                        // we have a ParentSpanId/ParentId, but no TraceId/SpanId, so default to use the ParentId for lookup
                        if (ActivityMappingById.TryGetValue(parentId, out ActivityMapping mapping))
                        {
                            parent = mapping.Scope.Span.Context;
                        }
                    }
                }

                if (parent is null && activeSpan is not null)
                {
                    // We ensure the activity follows the same TraceId as the span
                    // And marks the ParentId the current spanId
                    if ((activity.Parent is null || activity.Parent.StartTimeUtc <= activeSpan.StartTime.UtcDateTime)
                        && activitySpanId is not null
                        && activityTraceId is not null)
                    {
                        // TraceId (always 32 chars long even when using 64-bit ids)
                        w3cActivity.TraceId = activeSpan.Context.RawTraceId;
                        activityTraceId = w3cActivity.TraceId;

                        // SpanId (always 16 chars long)
                        w3cActivity.ParentSpanId = activeSpan.Context.RawSpanId;

                        // We clear internals Id and ParentId values to force recalculation.
                        w3cActivity.RawId = null;
                        w3cActivity.RawParentId = null;

                        // Avoid recalculation of the traceId.
                        traceId = activeSpan.TraceId128;
                    }
                }

                // if there's an existing Activity we try to use its TraceId and SpanId,
                // but if Activity.IdFormat is not ActivityIdFormat.W3C, they may be null or unparsable
                if (activityTraceId != null! && activitySpanId != null!)
                {
                    if (traceId == TraceId.Zero)
                    {
                        _ = HexString.TryParseTraceId(activityTraceId, out traceId);
                    }

                    _ = HexString.TryParseUInt64(activitySpanId, out spanId);

                    rawTraceId = activityTraceId;
                    rawSpanId = activitySpanId;
                }

                if (activityTraceId != null! && activitySpanId != null!)
                {
                    activityKey = activityTraceId + activitySpanId;
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
                if (IgnoreActivityHandler.IgnoreByOperationName(activity, activeSpan))
                {
                    activityMapping = default;
                    return;
                }

                activityKey ??= activity.Id;

                if (activityKey is null)
                {
                    // identified by Error Tracking
                    // unsure how exactly this occurs after reading through the Activity source code
                    // Activity.Id, Activity.SpanId and/or Activity.TraceId were null
                    // if this is the case, just ignore the Activity
                    activityMapping = default;
                    return;
                }

                activityMapping = ActivityMappingById.GetOrAdd(activityKey, _ => new(activity.Instance!, CreateScopeFromActivity(activity, tags, parent, traceId, spanId, rawTraceId, rawSpanId)));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStarted callback");
                activityMapping = default;
            }

            static Scope CreateScopeFromActivity(T activity, ITags? tags, SpanContext? parent, TraceId traceId, ulong spanId, string? rawTraceId, string? rawSpanId)
            {
                var span = Tracer.Instance.StartSpan(
                    activity.OperationName,
                    tags: tags,
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
                if (activity.Instance is not null)
                {
                    if (IgnoreActivityHandler.ShouldIgnoreByOperationName(activity))
                    {
                        return;
                    }

                    string key;
                    if (activity is IW3CActivity w3cActivity)
                    {
                        key = w3cActivity.TraceId + w3cActivity.SpanId;
                    }
                    else
                    {
                        key = activity.Id;
                    }

                    if (key is null)
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

                        CloseActivityScope(sourceName, activity, someValue.Scope);
                        return;
                    }
                }

                // The listener didn't send us the Activity or the scope instance was not found
                // In this case we are going go through the dictionary to check if we have an activity that
                // has been closed and then close the associated scope.
                if (activity.Instance is not null)
                {
                    if (Log.IsEnabled(LogEventLevel.Information))
                    {
                        Log.Information("DefaultActivityHandler.ActivityStopped: MISSING SCOPE [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]", new object[] { sourceName, activity!.Id, activity.RootId, activity.OperationName!, activity.StartTimeUtc, activity.Duration });
                    }
                }
                else
                {
                    Log.Information($"DefaultActivityHandler.ActivityStopped: [Missing Activity]");
                }

                List<string>? toDelete = null;
                foreach (var (activityId, item) in ActivityMappingById)
                {
                    var activityObject = item.Activity;
                    var scope = item.Scope;
                    var hasClosed = false;

                    if (activityObject.TryDuckCast<IActivity6>(out var activity6))
                    {
                        if (activity6.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity6, scope);
                            hasClosed = true;
                        }
                    }
                    else if (activityObject.TryDuckCast<IActivity5>(out var activity5))
                    {
                        if (activity5.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity5, scope);
                            hasClosed = true;
                        }
                    }
                    else if (activityObject.TryDuckCast<IActivity>(out var activity4))
                    {
                        if (activity4.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity4, scope);
                            hasClosed = true;
                        }
                    }

                    if (hasClosed)
                    {
                        toDelete ??= new List<string>();
                        toDelete.Add(activityId);
                    }
                }

                if (toDelete is not null)
                {
                    foreach (var item in toDelete)
                    {
                        ActivityMappingById.TryRemove(item, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the DefaultActivityHandler.ActivityStopped callback");
            }

            static void CloseActivityScope<TInner>(string sourceName, TInner activity, Scope scope)
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
        }
    }
}
