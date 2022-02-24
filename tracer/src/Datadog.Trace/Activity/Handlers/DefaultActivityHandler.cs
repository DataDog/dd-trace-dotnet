// <copyright file="DefaultActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        private static readonly Dictionary<object, Scope> ActivityScope = new();
        private static readonly string[] IgnoreOperationNamesStartingWith =
        {
            "System.Net.Http.",
            "Microsoft.AspNetCore.",
        };

        public bool ShouldListenTo(string sourceName, string version)
        {
            return true;
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            var activeSpan = (Span)Tracer.Instance.ActiveScope?.Span;

            // Propagate Trace and Parent Span ids
            ulong? traceId = null;
            ulong? spanId = null;
            if (activity is IActivity5 activity5)
            {
                if (activeSpan is not null)
                {
                    // If this is the first activity (no parent) and we already have an active span
                    // We ensure the activity follows the same TraceId as the span
                    // And marks the ParentId the current spanId
                    if (activity.Parent is null)
                    {
                        activity5.TraceId = activeSpan.TraceId.ToString("x32");
                        activity5.ParentSpanId = activeSpan.SpanId.ToString("x16");

                        // We clear internals Id and ParentId values to force recalculation.
                        activity5.RawId = null;
                        activity5.RawParentId = null;

                        // Avoid recalculation of the traceId.
                        traceId = activeSpan.TraceId;
                    }
                }

                // We convert the activity traceId and spanId to use it in the
                // Datadog span creation.
                traceId ??= Convert.ToUInt64(activity5.TraceId.Substring(16), 16);
                spanId = Convert.ToUInt64(activity5.SpanId, 16);
            }

            try
            {
                Log.Debug($"DefaultActivityHandler.ActivityStarted: [Source={sourceName}, Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);

                foreach (var ignoreSourceName in IgnoreOperationNamesStartingWith)
                {
                    if (activity.OperationName?.StartsWith(ignoreSourceName) == true)
                    {
                        if (activity is IActivity5 act5 && activeSpan is not null)
                        {
                            // If we ignore the activity and there's an existing active span
                            // We modify the activity spanId with the one in the span
                            // The reason for that is in case this ignored activity is used
                            // for propagation then the current active span will appear as parentId
                            // in the context propagation, and we will keep the entire trace.
                            act5.TraceId = activeSpan.TraceId.ToString("x32");
                            act5.SpanId = activeSpan.SpanId.ToString("x16");

                            // We clear internals Id and ParentId values to force recalculation.
                            act5.RawId = null;
                            act5.RawParentId = null;
                        }

                        return;
                    }
                }

                lock (ActivityScope)
                {
                    if (!ActivityScope.TryGetValue(activity.Instance, out _))
                    {
                        var span = Tracer.Instance.StartSpan(activity.OperationName, startTime: activity.StartTimeUtc, traceId: traceId, spanId: spanId);
                        var scope = Tracer.Instance.ActivateSpan(span, false);
                        ActivityScope[activity.Instance] = scope;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStarted callback");
            }
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            try
            {
                var hasActivity = activity?.Instance is not null;
                if (hasActivity)
                {
                    foreach (var ignoreSourceName in IgnoreOperationNamesStartingWith)
                    {
                        if (activity.OperationName?.StartsWith(ignoreSourceName) == true)
                        {
                            return;
                        }
                    }
                }

                lock (ActivityScope)
                {
                    if (hasActivity && ActivityScope.TryGetValue(activity.Instance, out var scope) && scope?.Span is not null)
                    {
                        // We have the exact scope associated with the Activity
                        Log.Debug($"DefaultActivityHandler.ActivityStopped: [Source={sourceName}, Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
                        CloseActivityScope(sourceName, activity, scope);
                        ActivityScope.Remove(activity.Instance);
                    }
                    else
                    {
                        // The listener didn't send us the Activity or the scope instance was not found
                        // In this case we are going go through the dictionary to check if we have an activity that
                        // has been closed and then close the associated scope.
                        if (hasActivity)
                        {
                            Log.Information($"DefaultActivityHandler.ActivityStopped: MISSING SCOPE [Source={sourceName}, Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
                        }
                        else
                        {
                            Log.Information($"DefaultActivityHandler.ActivityStopped: [Missing Activity]");
                        }

                        List<object> toDelete = null;
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
                                ActivityScope.Remove(item);
                            }
                        }
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
                            span.SetTag(Tags.SpanKind, "client");
                            break;
                        case ActivityKind.Consumer:
                            span.SetTag(Tags.SpanKind, "consumer");
                            break;
                        case ActivityKind.Internal:
                            span.SetTag(Tags.SpanKind, "internal");
                            break;
                        case ActivityKind.Producer:
                            span.SetTag(Tags.SpanKind, "producer");
                            break;
                        case ActivityKind.Server:
                            span.SetTag(Tags.SpanKind, "server");
                            break;
                    }
                }

                span.Finish(activity.StartTimeUtc.Add(activity.Duration));
                scope.Close();
            }
        }
    }
}
