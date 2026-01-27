// <copyright file="OtlpHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Helpers;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Activity
{
    internal static class OtlpHelpers
    {
        // see https://github.com/open-telemetry/opentelemetry-dotnet/blob/2916b2de80522d4b1cafe353b3fda3fd629ddb00/src/OpenTelemetry.Api/Internal/SemanticConventions.cs#LL109C25-L109C25
        internal const string OpenTelemetryException = "exception";
        internal const string OpenTelemetryErrorType = "exception.type";
        internal const string OpenTelemetryErrorMsg = "exception.message";
        internal const string OpenTelemetryErrorStack = "exception.stacktrace";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpHelpers));

        internal static void UpdateSpanFromActivity<TInner>(TInner activity, Span span)
            where TInner : IActivity
        {
            AgentConvertSpan(activity, span);
        }

        // See trace agent func convertSpan: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L459
        private static void AgentConvertSpan<TInner>(TInner activity, Span span)
            where TInner : IActivity
        {
            // This code path _should_ only be called from places where the span being closed was created with OTel tags
            // By predicating on that, we can make some performance optimizations and assumptions
            if (span.Tags is not OpenTelemetryTags tags)
            {
                Log.Error("Span closed in OtlpHelpers.AgentConvertSpan was unexpectedly of type {TagsType} instead of OpenTelemetryTags. Span tagging may be incorrect.", span.Tags.GetType());
                return;
            }

            // Perform activity casts first and check for null when their members need to be accessed
            var w3cActivity = activity as IW3CActivity;
            var activity5 = activity as IActivity5;
            var activity6 = activity as IActivity6;
            span.ResourceName = null; // Reset the resource name, it will be repopulated via the Datadog trace agent logic
            span.OperationName = null; // Reset the operation name, it will be repopulated

            // TODO: Add resources to spans
            // OpenTelemetry SDK resources are added to the span attributes by the configured exporter when OpenTelemetry.BaseExporter<T>.Export is called (e.g. OpenTelemetry.Exporter.ConsoleActivityExporter.Export)
            // **** Note: The exporter has a ParentProvider field that is populated with the TracerProviderSdk when everything is initially built, so this is technically per instance
            // **** To reliably get this, we might consider addding a Processor to the TracerProviderBuilder, though not sure where to invoke it
            // - service.instance.id
            // - service.name
            // - service.namespace
            // - service.version

            if (w3cActivity is not null)
            {
                tags.OtelTraceId = w3cActivity.TraceId;
            }

            // Copy over tags from Activity to the Datadog Span
            // Starting with .NET 5, Activity can hold tags whose value have type object?
            // For runtimes older than .NET 5, Activity can only hold tags whose values have type string
            // We use the ActivityTagsHelper instead of enumerating the collections directly to avoid allocating
            if (activity5 is not null)
            {
                if (activity5.HasTagObjects())
                {
                    var state = new OtelTagsEnumerationState(span);
                    ActivityEnumerationHelper.EnumerateTagObjects(activity5, ref state, static (ref s, kvp) =>
                    {
                        OtlpHelpers.SetTagObject(s.Span, kvp.Key, kvp.Value);
                        return true;
                    });
                }
            }
            else if (activity.HasTags())
            {
                var state = new OtelTagsEnumerationState(span);
                ActivityEnumerationHelper.EnumerateTags(activity, ref state, static (ref s, kvp) =>
                {
                    OtlpHelpers.SetTagObject(s.Span, kvp.Key, kvp.Value);
                    return true;
                });
            }

            // Fixup "version" tag
            var traceContext = span.Context.TraceContext;
            if (traceContext is not null
             && string.IsNullOrEmpty(traceContext.ServiceVersion)
             && span.GetTag("service.version") is { Length: > 0 } otelServiceVersion)
            {
                traceContext.ServiceVersion = otelServiceVersion;
            }

            // Additional Datadog policy: Set tag "span.kind"
            // Since the ActivityKind can only be one of a fixed set of values, always set the tag as prescribed by Datadog practices
            // even though the tag is not present on normal OTLP spans
            // Note that the "span.kind" is used to help craft an OperationName for the span (if present)
            if (activity5 is not null)
            {
                tags.SpanKind = GetSpanKind(activity5.Kind);
            }

            // Later: Support config 'span_name_as_resource_name'
            // Later: Support config 'span_name_remappings'
            if (string.IsNullOrEmpty(span.OperationName))
            {
                span.OperationName = OperationNameMapper.GetOperationName(tags);
            }

            // TODO: Add container tags from attributes if the tag isn't already in the span

            // Fixup "env" tag
            if (traceContext is not null
             && traceContext.Environment is null
             && span.GetTag("deployment.environment") is { Length: > 0 } otelServiceEnv)
            {
                traceContext.Environment = otelServiceEnv;
            }

            // TODO: The .NET OTLP exporter doesn't currently add tracestate, but the DD agent will set it as tag "w3c.tracestate" if detected
            // For now, we can keep this unimplemented so the resulting span matches the .NET OTLP exporter
            // span.SetTag("w3c.tracestate", w3CActivity.TraceStateString);

            // Add the library name and library version
            if (activity5 is not null)
            {
                // For .NET Activity .NET 5+ the Source.Name is only set via ActivitySource.StartActivity
                // and not when an Activity object is created manually and having .Start() called on it
                if (!string.IsNullOrEmpty(activity5.Source.Name))
                {
                    tags.OtelLibraryName = activity5.Source.Name;
                }

                if (!string.IsNullOrEmpty(activity5.Source.Version))
                {
                    tags.OtelLibraryVersion = activity5.Source.Version;
                }
            }

            // Set OTEL status code and OTEL status description
            if (tags.OtelStatusCode is null)
            {
                if (activity6 is not null)
                {
                    tags.OtelStatusCode = activity6.Status switch
                    {
                        ActivityStatusCode.Unset => "STATUS_CODE_UNSET",
                        ActivityStatusCode.Ok => "STATUS_CODE_OK",
                        ActivityStatusCode.Error => "STATUS_CODE_ERROR",
                        _ => "STATUS_CODE_UNSET"
                    };
                }
                else
                {
                    tags.OtelStatusCode = "STATUS_CODE_UNSET";
                }
            }

            // Map the OTEL status to error tags
            // See trace agent func status2Error: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L583
            if (activity6?.Status == ActivityStatusCode.Error)
            {
                AgentStatus2ErrorActivity6(activity6, span, tags);
            }
            else if (string.Equals(tags.OtelStatusCode, "STATUS_CODE_ERROR", StringComparison.Ordinal))
            {
                if (activity5 is not null)
                {
                    AgentStatus2ErrorActivity5(activity5, span);
                }
                else
                {
                    AgentStatus2ErrorBasic(activity, span);
                }
            }

            // Update Service with a reasonable default
            if (span.ServiceName is null)
            {
                span.ServiceName = span.GetTag("peer.service") switch
                {
                    string peerService when !string.IsNullOrEmpty(peerService) => peerService,
                    _ => "OTLPResourceNoServiceName",
                };
            }

            // Update Resource with a reasonable default
            if (span.ResourceName is null)
            {
                // TODO: Implement resourceFromTags
                // See: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L555

                // Fallback: Use the information provided by Activity
                if (activity5 is not null)
                {
                    span.ResourceName = activity5.DisplayName;
                }
                else
                {
                    span.ResourceName = activity.OperationName;
                }
            }

            // Update Type with a reasonable default
            if (string.IsNullOrWhiteSpace(span.Type))
            {
                span.Type = activity5 is null ? SpanTypes.Custom : AgentSpanKind2Type(activity5.Kind, span);
            }

            // extract any ActivityLinks and ActivityEvents
            if (activity5 is not null)
            {
                ExtractActivityLinks(span, activity5);
                ExtractActivityEvents(span, activity5);
            }
        }

        private static void ExtractActivityLinks<TInner>(Span span, TInner activity5)
            where TInner : IActivity5
        {
            // The underlying storage when there are no link is an empty array, so bail out in this scenario,
            // to avoid the allocations from boxing the enumerator
            if (!activity5.HasLinks())
            {
                return;
            }

            foreach (var link in activity5.Links)
            {
                if (!link.TryDuckCast<IActivityLink>(out var duckLink)
                 || duckLink.Context.TraceId.TraceId is null
                 || duckLink.Context.SpanId.SpanId is null)
                {
                    continue;
                }

                var parsedTraceId = HexString.TryParseTraceId(duckLink.Context.TraceId.TraceId, out var newActivityTraceId);
                var parsedSpanId = HexString.TryParseUInt64(duckLink.Context.SpanId.SpanId, out var newActivitySpanId);

                if (!parsedTraceId || !parsedSpanId)
                {
                    continue;
                }

                var traceParentSample = duckLink.Context.TraceFlags > 0;
                var traceState = W3CTraceContextPropagator.ParseTraceState(duckLink.Context.TraceState);
                var traceTags = TagPropagation.ParseHeader(traceState.PropagatedTags);
                var samplingPriority = traceParentSample switch
                {
                    true when traceState.SamplingPriority != null && SamplingPriorityValues.IsKeep(traceState.SamplingPriority.Value) => traceState.SamplingPriority.Value,
                    true => SamplingPriorityValues.AutoKeep,
                    false when traceState.SamplingPriority != null && SamplingPriorityValues.IsDrop(traceState.SamplingPriority.Value) => traceState.SamplingPriority.Value,
                    false => SamplingPriorityValues.AutoReject
                };

                if (traceParentSample && SamplingPriorityValues.IsDrop(samplingPriority))
                {
                    traceTags.SetTag(Tags.Propagated.DecisionMaker, "-0");
                }
                else if (!traceParentSample && SamplingPriorityValues.IsKeep(samplingPriority))
                {
                    traceTags.RemoveTag(Tags.Propagated.DecisionMaker);
                }

                var spanContext = new SpanContext(
                    newActivityTraceId,
                    newActivitySpanId,
                    samplingPriority: samplingPriority,
                    serviceName: null,
                    origin: traceState.Origin,
                    isRemote: duckLink.Context.IsRemote);

                spanContext.AdditionalW3CTraceState = traceState.AdditionalValues;
                spanContext.LastParentId = traceState.LastParent;
                spanContext.PropagatedTags = traceTags;

                List<KeyValuePair<string, string>>? attributes = null;
                if (duckLink.Tags is not null)
                {
                    foreach (var kvp in duckLink.Tags)
                    {
                        if (!string.IsNullOrEmpty(kvp.Key)
                         && IsAllowedAttributeType(kvp.Value))
                        {
                            if (kvp.Value is Array array)
                            {
                                int index = 0;
                                foreach (var item in array)
                                {
                                    if (item?.ToString() is { } value)
                                    {
                                        attributes ??= new();
                                        attributes.Add(new($"{kvp.Key}.{index}", value));
                                        index++;
                                    }
                                }
                            }
                            else if (kvp.Value?.ToString() is { } kvpValue)
                            {
                                attributes ??= new();
                                attributes.Add(new(kvp.Key, kvpValue));
                            }
                        }
                    }
                }

                span.AddLink(new SpanLink(spanContext, attributes));
            }
        }

        private static void ExtractActivityEvents<TInner>(Span span, TInner activity5)
            where TInner : IActivity5
        {
            // The underlying storage when there are no link is an empty array, so bail out in this scenario,
            // to avoid the allocations from boxing the enumerator
            if (!activity5.HasEvents())
            {
                return;
            }

            foreach (var activityEvent in activity5.Events)
            {
                if (!activityEvent.TryDuckCast<ActivityEvent>(out var duckEvent))
                {
                    continue;
                }

                List<KeyValuePair<string, object>>? eventAttributes = null;
                foreach (var kvp in duckEvent.Tags)
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value is not null)
                    {
                        eventAttributes ??= new();
                        eventAttributes.Add(new(kvp.Key, kvp.Value));
                    }
                }

                span.AddEvent(new SpanEvent(duckEvent.Name, duckEvent.Timestamp, eventAttributes));
            }
        }

        internal static string GetSpanKind(ActivityKind activityKind) =>
            activityKind switch
            {
                ActivityKind.Server => SpanKinds.Server,
                ActivityKind.Client => SpanKinds.Client,
                ActivityKind.Producer => SpanKinds.Producer,
                ActivityKind.Consumer => SpanKinds.Consumer,
                _ => SpanKinds.Internal,
            };

        /// <summary>
        /// Add the provided Otel <paramref name="key"/> and <paramref name="value"/> on the <paramref name="span"/>.
        /// Depending on the <paramref name="key"/>, this may set a metric, a tag, a tag with a different value
        /// (such as <see cref="Tags.HttpStatusCode"/>, or it may set a standard property such as <see cref="Span.OperationName"/>
        /// </summary>
        /// <param name="span">The span to set the tag on</param>
        /// <param name="key">The OTel tag key</param>
        /// <param name="value">The OTel tag value</param>
        /// <param name="allowUnrolling">When enabled, enumerable values will be set as multiple indexed tags, e.g. (key[0], value0), (key[1], value1) </param>
        /// <param name="setKnownValues">When enabled, the key value can be used to set "standard" properties, such as <see cref="Span.OperationName"/>.
        /// When disabled, tags that would otherwise set these values are ignored. </param>
        internal static void SetTagObject(Span span, string key, object? value, bool allowUnrolling = true, bool setKnownValues = true)
        {
            if (value is null)
            {
                span.SetTag(key, null);
                return;
            }

            switch (value)
            {
                case char c:
                    AgentSetOtlpTag(span, key, c.ToString());
                    break;
                case string s:
                    AgentSetOtlpTag(span, key, s);
                    break;
                case bool b:
                    AgentSetOtlpTag(span, key, b ? "true" : "false");
                    break;
                case byte b:
                    span.SetMetric(key, b);
                    break;
                case sbyte sb:
                    span.SetMetric(key, sb);
                    break;
                case short sh:
                    span.SetMetric(key, sh);
                    break;
                case ushort us:
                    span.SetMetric(key, us);
                    break;
                case int i: // TODO: Can't get here from OTEL API, test with Activity API
                    // special case where we need to remap "http.response.status_code" and the deprecated "http.status_code"
                    if (key == "http.response.status_code" || key == "http.status_code")
                    {
                        if (setKnownValues)
                        {
                            span.SetTag(Tags.HttpStatusCode, i.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    else
                    {
                        span.SetMetric(key, i);
                    }

                    break;
                case uint ui:
                    span.SetMetric(key, ui);
                    break;
                case long l:
                    span.SetMetric(key, l);
                    break;
                case ulong ul:
                    span.SetMetric(key, ul);
                    break;
                case float f:
                    span.SetMetric(key, f);
                    break;
                case double d:
                    span.SetMetric(key, d);
                    break;
                case IEnumerable enumerable:
                    if (allowUnrolling)
                    {
                        var index = 0;
                        foreach (var element in (enumerable))
                        {
                            // we are only supporting a single level of unrolling
                            SetTagObject(span, $"{key}.{index}", element, allowUnrolling: false);
                            index++;
                        }

                        if (index == 0)
                        {
                            // indicates that it was an empty array, we need to add the tag
                            AgentSetOtlpTag(span, key, "[]");
                        }
                    }
                    else
                    {
                        // we've already unrolled once, don't do it again for IEnumerable values
                        AgentSetOtlpTag(span, key, JsonConvert.SerializeObject(value));
                    }

                    break;
                default:
                    AgentSetOtlpTag(span, key, value.ToString());
                    break;
            }
        }

        // See trace agent func setMetaOTLP: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L424
        internal static void AgentSetOtlpTag(Span span, string key, string? value, bool setKnownValues = true)
        {
            switch (key)
            {
                case "operation.name":
                    if (setKnownValues)
                    {
                        span.OperationName = value?.ToLowerInvariant();
                    }

                    break;
                case "service.name":
                    if (setKnownValues)
                    {
                        span.ServiceName = value;
                    }

                    break;
                case "resource.name":
                    if (setKnownValues)
                    {
                        span.ResourceName = value;
                    }

                    break;
                case "span.type":
                    if (setKnownValues)
                    {
                        span.Type = value;
                    }

                    break;
                case "analytics.event":
                    if (GoStrConvParseBool(value) is bool b)
                    {
                        span.SetMetric(Tags.Analytics, b ? 1 : 0);
                    }

                    break;
                case "otel.status_code":
                    if (setKnownValues)
                    {
                        var newStatusCodeString = value switch
                        {
                            null => "STATUS_CODE_UNSET",
                            "ERROR" => "STATUS_CODE_ERROR",
                            "UNSET" => "STATUS_CODE_UNSET",
                            "OK" => "STATUS_CODE_OK",
                            string s => s,
                        };
                        span.SetTag(key, newStatusCodeString);
                    }

                    break;
                case "http.response.status_code":
                    if (setKnownValues)
                    {
                        span.SetTag(Tags.HttpStatusCode, value);
                    }

                    break;
                default:
                    span.SetTag(key, value);
                    break;
            }
        }

        // See trace agent func status2Error: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L583
        private static void AgentStatus2ErrorActivity6<TInner>(TInner activity, Span span, OpenTelemetryTags tags)
            where TInner : IActivity6
        {
            span.Error = true;

            // First iterate through Activity events first and set error.msg, error.type, and error.stack
            ExtractExceptionAttributes(activity, span);

            if (span.GetTag(Tags.ErrorMsg) is null)
            {
                span.SetTag(Tags.ErrorMsg, activity.StatusDescription);
            }
        }

        private static void AgentStatus2ErrorActivity5<TInner>(TInner activity, Span span)
            where TInner : IActivity5
        {
            ExtractExceptionAttributes(activity, span);
            AgentStatus2ErrorBasic(activity, span);
        }

        private static void AgentStatus2ErrorBasic<TInner>(TInner activity, Span span)
            where TInner : IActivity
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

        /// <summary>
        /// Iterates through <c>Activity.Events</c> looking for a <see cref="OpenTelemetryException"/> to copy over
        /// to the <see cref="Span.Tags"/> of <paramref name="span"/>. The span tags will reflect the last exception
        /// event.
        /// </summary>
        /// <param name="activity">The <see cref="IActivity"/> to check for the exception event data.</param>
        /// <param name="span">The <see cref="Span"/> to copy the exception event data to.</param>
        /// <typeparam name="TInner">
        /// The type of <c>Activity</c> - note that only <see cref="IActivity5"/> and up have support for events.
        /// </typeparam>
        /// <remarks>OpenTelemetry creates these attributes via it's <c>Activity.RecordException</c> function.</remarks>
        private static void ExtractExceptionAttributes<TInner>(TInner activity, Span span)
            where TInner : IActivity5
        {
            // OpenTelemetry stores the exception attributes in Activity.Events
            // Activity.Events was only added in .NET 5+, which maps to our IActivity5 & IActivity6
            if (!activity.HasEvents())
            {
                return;
            }

            foreach (var activityEvent in activity.Events)
            {
                if (!activityEvent.TryDuckCast<ActivityEvent>(out var duckEvent))
                {
                    continue;
                }

                if (duckEvent.Name != OpenTelemetryException)
                {
                    continue;
                }

                string? errorType = null;
                string? errorMsg = null;
                string? errorStack = null;

                foreach (var tag in duckEvent.Tags)
                {
                    switch (tag.Key)
                    {
                        case OpenTelemetryErrorType:
                            errorType = tag.Value?.ToString();
                            break;

                        case OpenTelemetryErrorMsg:
                            errorMsg = tag.Value?.ToString();
                            break;

                        case OpenTelemetryErrorStack:
                            errorStack = tag.Value?.ToString();
                            break;
                    }
                }

                // Ensure that all of the error tracking tags are updated (even null values) from the same exception attribute
                SetTagObject(span, Tags.ErrorType, errorType);
                SetTagObject(span, Tags.ErrorMsg, errorMsg);
                SetTagObject(span, Tags.ErrorStack, errorStack);
            }
        }

        // See trace agent func spanKind2Type: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L621
        internal static string AgentSpanKind2Type(ActivityKind kind, Span span) => kind switch
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

        // This algorithm implements Go function strconv.ParseBool, which the trace agent uses
        // for string->bool conversions
        // See https://pkg.go.dev/strconv#ParseBool
        private static bool? GoStrConvParseBool(string? value)
        {
            if (value is null || value.Length == 0)
            {
                return null;
            }

            if (value.Length == 1)
            {
                if (value[0] == 't' || value[0] == 'T' ||
                    value[0] == '1')
                {
                    return true;
                }

                if (value[0] == 'f' || value[0] == 'F' ||
                    value[0] == '0')
                {
                    return false;
                }

                return null;
            }

            if (value == "TRUE" || value == "true" || value == "True")
            {
                return true;
            }

            if (value == "FALSE" || value == "false" || value == "False")
            {
                return false;
            }

            return null;
        }

        private static bool IsAllowedAttributeType(object? value)
        {
            if (value is null)
            {
                return false;
            }

            if (value is Array array)
            {
                if (array.Length == 0 ||
                    array.Rank > 1)
                {
                    // Newtonsoft doesn't seem to support multidimensional arrays (e.g., [,]), but does support jagged (e.g., [][])
                    return false;
                }

                if (value.GetType() is { } type
                 && type.IsArray
                 && type.GetElementType() == typeof(object))
                {
                    // Arrays may only have a primitive type, not 'object'
                    return false;
                }

                value = array.GetValue(0);

                if (value is null)
                {
                    return false;
                }
            }

            return (value is string or bool ||
                    value is char ||
                    value is sbyte ||
                    value is byte ||
                    value is ushort ||
                    value is short ||
                    value is uint ||
                    value is int ||
                    value is ulong ||
                    value is long ||
                    value is float ||
                    value is double ||
                    value is decimal);
        }
    }
}
