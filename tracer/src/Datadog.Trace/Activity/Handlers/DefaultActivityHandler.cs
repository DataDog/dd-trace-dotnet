// <copyright file="DefaultActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// The default handler catches an activity and creates a datadog span from it.
    /// </summary>
    internal class DefaultActivityHandler : IActivityHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DefaultActivityHandler));
        private static readonly ConcurrentDictionary<object, Scope> ActivityScope = new();

        public bool ShouldListenTo(string sourceName, string? version)
        {
            return true;
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            var activeSpan = (Span?)Tracer.Instance.ActiveScope?.Span;

            // Propagate Trace and Parent Span ids
            ulong? traceId = null;
            ulong? spanId = null;
            string? rawTraceId = null;
            string? rawSpanId = null;
            if (activity is IW3CActivity w3cActivity)
            {
                if (activeSpan is not null)
                {
                    // If this is the first activity (no parent) and we already have an active span
                    // or the span was started after the parent activity so we use the span as a parent

                    // We ensure the activity follows the same TraceId as the span
                    // And marks the ParentId the current spanId

                    if (activity.Parent is null || activity.Parent.StartTimeUtc < activeSpan.StartTime.UtcDateTime)
                    {
                        // TraceId
                        w3cActivity.TraceId = string.IsNullOrWhiteSpace(activeSpan.Context.RawTraceId) ?
                                                  activeSpan.TraceId.ToString("x32") : activeSpan.Context.RawTraceId;

                        // SpanId
                        w3cActivity.ParentSpanId = string.IsNullOrWhiteSpace(activeSpan.Context.RawSpanId) ?
                                                       activeSpan.SpanId.ToString("x16") : activeSpan.Context.RawSpanId;

                        // We clear internals Id and ParentId values to force recalculation.
                        w3cActivity.RawId = null;
                        w3cActivity.RawParentId = null;

                        // Avoid recalculation of the traceId.
                        traceId = activeSpan.TraceId;
                    }
                }

                // We convert the activity traceId and spanId to use it in the
                // Datadog span creation.
                traceId ??= Convert.ToUInt64(w3cActivity.TraceId.Substring(16), 16);
                spanId = Convert.ToUInt64(w3cActivity.SpanId, 16);
                rawTraceId = w3cActivity.TraceId;
                rawSpanId = w3cActivity.SpanId;
            }

            try
            {
                Log.Debug($"DefaultActivityHandler.ActivityStarted: [Source={sourceName}, Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);

                // We check if we have to ignore the activity by the operation name value
                if (IgnoreActivityHandler.IgnoreByOperationName(activity, activeSpan))
                {
                    return;
                }

                ActivityScope.GetOrAdd(activity.Instance, _ => CreateScopeFromActivity(activity, traceId, spanId, rawTraceId, rawSpanId));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStarted callback");
            }

            static Scope CreateScopeFromActivity(T activity, ulong? traceId, ulong? spanId, string? rawTraceId, string? rawSpanId)
            {
                var span = Tracer.Instance.StartSpan(activity.OperationName, startTime: activity.StartTimeUtc, traceId: traceId, spanId: spanId, rawTraceId: rawTraceId, rawSpanId: rawSpanId);
                var scope = Tracer.Instance.ActivateSpan(span, false);
                return scope;
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

                    if (ActivityScope.TryRemove(activity.Instance, out var scope) && scope?.Span is not null)
                    {
                        // We have the exact scope associated with the Activity
                        Log.Debug($"DefaultActivityHandler.ActivityStopped: [Source={sourceName}, Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
                        CloseActivityScope(sourceName, activity, scope);
                        return;
                    }
                }

                // The listener didn't send us the Activity or the scope instance was not found
                // In this case we are going go through the dictionary to check if we have an activity that
                // has been closed and then close the associated scope.
                if (activity.Instance is not null)
                {
                    Log.Information($"DefaultActivityHandler.ActivityStopped: MISSING SCOPE [Source={sourceName}, Id={activity!.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
                }
                else
                {
                    Log.Information($"DefaultActivityHandler.ActivityStopped: [Missing Activity]");
                }

                List<object>? toDelete = null;
                foreach (var item in ActivityScope)
                {
                    var activityObject = item.Key;
                    var hasClosed = false;

                    if (activityObject.TryDuckCast<IActivity6>(out var activity6))
                    {
                        if (activity6.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity6, item.Value);
                            hasClosed = true;
                        }
                    }
                    else if (activityObject.TryDuckCast<IActivity5>(out var activity5))
                    {
                        if (activity5.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity5, item.Value);
                            hasClosed = true;
                        }
                    }
                    else if (activityObject.TryDuckCast<IActivity>(out var activity4))
                    {
                        if (activity4.Duration != TimeSpan.Zero)
                        {
                            CloseActivityScope(sourceName, activity4, item.Value);
                            hasClosed = true;
                        }
                    }

                    if (hasClosed)
                    {
                        toDelete ??= new List<object>();
                        toDelete.Add(activityObject);
                    }
                }

                if (toDelete is not null)
                {
                    foreach (var item in toDelete)
                    {
                        ActivityScope.TryRemove(item, out _);
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
                foreach (var activityTag in activity.Tags)
                {
                    span.SetTag(activityTag.Key, activityTag.Value);
                }

                foreach (var activityBag in activity.Baggage)
                {
                    span.SetTag(activityBag.Key, activityBag.Value);
                }

                if (activity is IActivity6 { Status: ActivityStatusCode.Error } activity6)
                {
                    span.Error = true;
                    span.SetTag(Tags.ErrorMsg, activity6.StatusDescription);
                }

                if (activity is IActivity5 activity5)
                {
                    if (!string.IsNullOrWhiteSpace(sourceName))
                    {
                        span.SetTag("source", sourceName);
                        span.ResourceName = $"{sourceName}.{span.OperationName}";
                    }
                    else
                    {
                        span.ResourceName = span.OperationName;
                    }

                    switch (activity5.Kind)
                    {
                        case ActivityKind.Client:
                            span.SetTag(Tags.SpanKind, SpanKinds.Client);
                            break;
                        case ActivityKind.Consumer:
                            span.SetTag(Tags.SpanKind, SpanKinds.Consumer);
                            break;
                        case ActivityKind.Producer:
                            span.SetTag(Tags.SpanKind, SpanKinds.Producer);
                            break;
                        case ActivityKind.Server:
                            span.SetTag(Tags.SpanKind, SpanKinds.Server);
                            break;
                    }
                }

                span.Finish(activity.StartTimeUtc.Add(activity.Duration));
                scope.Close();
            }
        }
    }
}
