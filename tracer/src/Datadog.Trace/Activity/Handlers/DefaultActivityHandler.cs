// <copyright file="DefaultActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
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
        private static readonly ConcurrentDictionary<string, ActivityMapping> ActivityMappingById = new();
        private static readonly string[] SpanKindNames = new string[]
        {
            "internal",
            "server",
            "client",
            "producer",
            "consumer",
        };

        public bool ShouldListenTo(string sourceName, string? version)
        {
            return true;
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            var activeSpan = (Span?)Tracer.Instance.ActiveScope?.Span;

            // Propagate Trace and Parent Span ids
            SpanContext? parent = null;
            ulong? traceId = null;
            ulong? spanId = null;
            string? rawTraceId = null;
            string? rawSpanId = null;

            // If the user has specified a parent context, get the parent Datadog SpanContext
            if (activity.ParentId is string parentId
                && ActivityMappingById.TryGetValue(parentId, out ActivityMapping mapping))
            {
                parent = mapping.Scope.Span.Context;
            }

            if (activity is IW3CActivity w3cActivity)
            {
                if (parent is null && activeSpan is not null)
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

                ActivityMappingById.GetOrAdd(activity.Id, _ => new(activity.Instance, CreateScopeFromActivity(activity, parent, traceId, spanId, rawTraceId, rawSpanId)));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing the OnActivityStarted callback");
            }

            static Scope CreateScopeFromActivity(T activity, SpanContext? parent, ulong? traceId, ulong? spanId, string? rawTraceId, string? rawSpanId)
            {
                string? serviceName = activity switch
                {
                    IActivity5 activity5 => activity5.Source.Name,
                    _ => null
                };

                var span = Tracer.Instance.StartSpan(activity.OperationName, parent: parent, serviceName: serviceName, startTime: activity.StartTimeUtc, traceId: traceId, spanId: spanId, rawTraceId: rawTraceId, rawSpanId: rawSpanId);
                span.ResourceName = activity.OperationName;
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

                    if (ActivityMappingById.TryRemove(activity.Id, out ActivityMapping value) && value.Scope?.Span is not null)
                    {
                        // We have the exact scope associated with the Activity
                        Log.Debug($"DefaultActivityHandler.ActivityStopped: [Source={sourceName}, Id={activity.Id}, RootId={activity.RootId}, OperationName={{OperationName}}, StartTimeUtc={{StartTimeUtc}}, Duration={{Duration}}]", activity.OperationName, activity.StartTimeUtc, activity.Duration);
                        CloseActivityScope(sourceName, activity, value.Scope);
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

                List<string>? toDelete = null;
                foreach (var item in ActivityMappingById)
                {
                    string activityId = item.Key;
                    var activityObject = item.Value.Activity;
                    var scope = item.Value.Scope;
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
}
