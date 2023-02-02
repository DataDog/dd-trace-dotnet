// <copyright file="DefaultActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// The default handler catches an activity and creates a datadog span from it.
    /// </summary>
    internal class DefaultActivityHandler : IActivityHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DefaultActivityHandler));
        internal static readonly ConcurrentDictionary<string, ActivityMapping> ActivityMappingById = new();
        private static readonly IntegrationId IntegrationId = IntegrationId.OpenTelemetry;

        public bool ShouldListenTo(string sourceName, string? version)
        {
            return true;
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            Tracer.Instance.TracerManager.Telemetry.IntegrationRunning(IntegrationId);
            var activeSpan = (Span?)Tracer.Instance.ActiveScope?.Span;

            // Propagate Trace and Parent Span ids
            SpanContext? parent = null;
            TraceId traceId = 0;
            ulong spanId = 0;
            string? rawTraceId = null;
            string? rawSpanId = null;

            // for non-IW3CActivity interfaces we'll use Activity.Id as the key as they don't have a guaranteed TraceId+SpanId
            // for IW3CActivity interfaces we'll use the Activity.TraceId + Activity.SpanId as the key
            // have to also validate that the TraceId and SpanId actually exist and aren't null - as they can be in some cases
            string? activityKey = null;

            if (activity is IW3CActivity w3cActivity)
            {
                if (w3cActivity.TraceId is { } activityTraceId && w3cActivity.SpanId is { } activitySpanId)
                {
                    activityKey = activityTraceId + activitySpanId;
                }

                // If the user has specified a parent context, get the parent Datadog SpanContext
                if (w3cActivity.ParentSpanId != null!
                    && w3cActivity.ParentId is { } parentId)
                {
                    // we know that we have a parent context, but we use TraceId+ParentSpanId for the mapping
                    // This is a result of an issue with OTel v1.0.1 (unsure if OTel or us tbh) where the
                    // ".ParentId" matched for the Trace+Span IDs but not for the flags portion
                    // Doing a lookup on just the TraceId+ParentSpanId seems to be more resilient
                    if (w3cActivity.TraceId is { } && w3cActivity.ParentSpanId is { } parentSpanId)
                    {
                        if (ActivityMappingById.TryGetValue(w3cActivity.TraceId + w3cActivity.ParentSpanId, out ActivityMapping mapping))
                        {
                            parent = mapping.Scope.Span.Context;
                        }
                        else
                        {
                            // create a new parent span context for the ActivityContext
#if NETCOREAPP
                            _ = HexString.TryParseUInt64(w3cActivity.TraceId.AsSpan(16), out var newActivityTraceId);
                            _ = HexString.TryParseUInt64(w3cActivity.ParentSpanId, out var newActivitySpanId);
#else
                            _ = HexString.TryParseUInt64(w3cActivity.TraceId.Substring(16), out var newActivityTraceId);
                            _ = HexString.TryParseUInt64(w3cActivity.ParentSpanId, out var newActivitySpanId);
#endif
                            parent = Tracer.Instance.CreateSpanContext(SpanContext.None, traceId: newActivityTraceId, spanId: newActivitySpanId, rawTraceId: w3cActivity.TraceId, rawSpanId: w3cActivity.ParentSpanId);
                        }
                    }
                    else
                    {
                        // we have a ParentSpanId/ParentId, but no TraceId/SpanId, so default to use the ParentId for lookup
                        if (ActivityMappingById.TryGetValue(w3cActivity.ParentId, out ActivityMapping mapping))
                        {
                            parent = mapping.Scope.Span.Context;
                        }
                    }
                }

                if (parent is null && activeSpan is not null)
                {
                    // We ensure the activity follows the same TraceId as the span
                    // And marks the ParentId the current spanId

                    if (activity.Parent is null || activity.Parent.StartTimeUtc < activeSpan.StartTime.UtcDateTime)
                    {
                        // TraceId (always 32 chars long even when using 64-bit ids)
                        w3cActivity.TraceId = string.IsNullOrWhiteSpace(activeSpan.Context.RawTraceId) ?
                                                  HexString.ToHexString(activeSpan.TraceId, pad16To32: true) :
                                                  activeSpan.Context.RawTraceId;

                        // SpanId (always 16 chars long)
                        w3cActivity.ParentSpanId = string.IsNullOrWhiteSpace(activeSpan.Context.RawSpanId) ?
                                                       HexString.ToHexString(activeSpan.SpanId) :
                                                       activeSpan.Context.RawSpanId;

                        // We clear internals Id and ParentId values to force recalculation.
                        w3cActivity.RawId = null;
                        w3cActivity.RawParentId = null;

                        // Avoid recalculation of the traceId.
                        traceId = activeSpan.TraceId;
                    }
                }

                // if there's an existing Activity we try to use its TraceId and SpanId,
                // but if Activity.IdFormat is not ActivityIdFormat.W3C, they may be null or unparsable
                if (w3cActivity is { TraceId: { } activityTraceId, SpanId: { } activitySpanId })
                {
                    if (traceId == 0)
                    {
                        _ = HexString.TryParseTraceId(activityTraceId, out traceId);
                    }

                    _ = HexString.TryParseUInt64(activitySpanId, out spanId);

                    rawTraceId = activityTraceId;
                    rawSpanId = activitySpanId;
                }
            }

            try
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("DefaultActivityHandler.ActivityStarted: [Source={SourceName}, Id={Id}, RootId={RootId}, OperationName={OperationName}, StartTimeUtc={StartTimeUtc}, Duration={Duration}]", new object[] { sourceName, activity.Id, activity.RootId, activity.OperationName!, activity.StartTimeUtc, activity.Duration });
                }

                // We check if we have to ignore the activity by the operation name value
                if (IgnoreActivityHandler.IgnoreByOperationName(activity, activeSpan))
                {
                    return;
                }

                activityKey ??= activity.Id;
                ActivityMappingById.GetOrAdd(activityKey, _ => new(activity.Instance!, CreateScopeFromActivity(activity, parent, traceId, spanId, rawTraceId, rawSpanId)));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStarted callback");
            }

            static Scope CreateScopeFromActivity(T activity, SpanContext? parent, TraceId traceId, ulong spanId, string? rawTraceId, string? rawSpanId)
            {
                var span = Tracer.Instance.StartSpan(activity.OperationName, parent: parent, startTime: activity.StartTimeUtc, traceId: traceId, spanId: spanId, rawTraceId: rawTraceId, rawSpanId: rawSpanId);
                Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
                return Tracer.Instance.ActivateSpan(span, false);
            }
        }

        public void ActivityStopped<T>(string sourceName, T activity)
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

        public readonly struct ActivityMapping
        {
            public readonly object Activity;
            public readonly Scope Scope;

            internal ActivityMapping(object activity, Scope scope)
            {
                Activity = activity;
                Scope = scope;
            }
        }
    }

#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable SA1402 // File may only contain a single type
    internal static class ActivityMappingExtensions
    {
        public static void Deconstruct(this KeyValuePair<string, DefaultActivityHandler.ActivityMapping> item, out string key, out DefaultActivityHandler.ActivityMapping value)
        {
            key = item.Key;
            value = item.Value;
        }
    }
}
