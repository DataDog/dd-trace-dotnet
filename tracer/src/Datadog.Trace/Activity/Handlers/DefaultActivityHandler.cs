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

                // TODO: The OpenTelemetry SDK can create spans without making them active. We'll need automatic instrumentation to detect when we want to update the ActiveScope or not
                // Worst case, we set Tracer.ActiveScope here unconditionally so it can be retrieved by Tracer.ActiveScope from the original API methods
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
                span.OperationName = null; // Reset the operation name, it will be recalculated by the trace agent OTLP logic

                // Copy over tags from Activity to the Datadog Span
                // Starting with .NET 5, Activity can hold tags whose value have type object?
                // For runtimes older than .NET 5, Activity can only hold tags whose values have type string
                if (activity is IActivity5 activity5ObjectTags)
                {
                    foreach (var activityTag in activity5ObjectTags.TagObjects)
                    {
                        OtlpHelpers.SetTagObject(span, activityTag.Key, activityTag.Value);
                    }
                }
                else
                {
                    foreach (var activityTag in activity.Tags)
                    {
                        OtlpHelpers.SetTagObject(span, activityTag.Key, activityTag.Value);
                    }
                }

                foreach (var activityBag in activity.Baggage)
                {
                    span.SetTag(activityBag.Key, activityBag.Value);
                }

                // TODO: Implement status2Error
                if (activity is IActivity6 { Status: ActivityStatusCode.Error } activity6)
                {
                    span.Error = true;
                    if (span.GetTag(Tags.ErrorMsg) is null)
                    {
                        span.SetTag(Tags.ErrorMsg, activity6.StatusDescription);
                    }
                }
                else if (span.GetTag("otel.status_code") == "STATUS_CODE_ERROR")
                {
                    span.Error = true;
                    if (span.GetTag(Tags.ErrorMsg) is null)
                    {
                        if (span.GetTag("otel.status_description") is string statusDescription)
                        {
                            span.SetTag(Tags.ErrorMsg, statusDescription);
                        }
                        else if (span.GetTag("http.status_code") is string statusCodeString)
                        {
                            if (span.GetTag("http.status_text") is string httpTextString)
                            {
                                span.SetTag(Tags.ErrorMsg, $"{statusCodeString} {httpTextString}");
                            }
                            else
                            {
                                span.SetTag(Tags.ErrorMsg, statusCodeString);
                            }
                        }
                    }
                }
                else if (span.GetTag("otel.status_code") is null)
                {
                    span.SetTag("otel.status_code", "STATUS_CODE_UNSET");
                }

                if (activity is IActivity5 activity5)
                {
                    span.ResourceName = activity5.DisplayName;
                    span.SetTag("otel.library.name", activity5.Source.Name);
                    span.SetTag("otel.library.version", activity5.Source.Version);

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

                    if (span.OperationName is null)
                    {
                        // Later: Support config 'span_name_as_resource_name'
                        // Later: Support config 'span_name_remappings'
                        span.OperationName = activity5.Source.Name switch
                        {
                            string libName when !string.IsNullOrEmpty(libName) => $"{libName}.{SpanKindNames[(int)activity5.Kind]}",
                            _ => $"opentelemetry.{SpanKindNames[(int)activity5.Kind]}",
                        };
                    }

                    // Fixup Type
                    if (string.IsNullOrWhiteSpace(span.Type))
                    {
                        span.Type = activity5.Kind switch
                        {
                            ActivityKind.Server => SpanTypes.Web,
                            ActivityKind.Client => span.GetTag("db.system") switch
                            {
                                "redis" or "memcached" => "cache",
                                string => "db",
                                _ => "http",
                            },
                            _ => SpanTypes.Custom,
                        };
                    }
                }
                else
                {
                    span.OperationName ??= activity.OperationName;
                }

                // OpenTelemtry SDK / OTLP Fixups
                // 1) Update the Span.Type based off some heuristics

                // TODO: Finish
                // 2) Use trace agent algorithm to populate Datadog span from the Otlp attributes
                //    See https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L459
                // - "events" tag // TODO
                if (activity is IW3CActivity w3CActivity)
                {
                    span.SetTag("otel.trace_id", w3CActivity.TraceId);

                    // TODO: The .NET OTLP exporter doesn't currently add tracestate, but the DD agent will set it as tag "w3c.tracestate" if detected
                    // For now, we can keep this unimplemented so the resulting span matches the .NET OTLP exporter
                    // span.SetTag("w3c.tracestate", w3CActivity.TraceStateString);
                }

                // Fixup "version" tag
                if (Tracer.Instance.Settings.ServiceVersion is null
                    && span.GetTag("service.version") is string otelServiceVersion
                    && !string.IsNullOrEmpty(otelServiceVersion))
                {
                    span.SetTag(Tags.Version, otelServiceVersion);
                }

                // Fixup "env" tag
                if (Tracer.Instance.Settings.Environment is null
                    && span.GetTag("deployment.environment") is string otelServiceEnv
                    && !string.IsNullOrEmpty(otelServiceEnv))
                {
                    span.SetTag(Tags.Env, otelServiceEnv);
                }

                // Fixup Service property
                if (span.ServiceName is null)
                {
                    span.ServiceName = span.GetTag("peer.service") switch
                    {
                        string peerService when !string.IsNullOrEmpty(peerService) => peerService,
                        _ => "OTLPResourceNoServiceName",
                    };
                }

                // Fixup Resource property
                if (span.ResourceName is null)
                {
                    // TODO: Implement resourceFromTags
                    // See: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L555
                }

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
