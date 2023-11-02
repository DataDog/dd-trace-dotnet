// <copyright file="OtlpHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Text;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Activity
{
    internal static class OtlpHelpers
    {
        // see https://github.com/open-telemetry/opentelemetry-dotnet/blob/2916b2de80522d4b1cafe353b3fda3fd629ddb00/src/OpenTelemetry.Api/Internal/SemanticConventions.cs#LL109C25-L109C25
        internal const string OpenTelemetryException = "exception";
        internal const string OpenTelemetryErrorType = "exception.type";
        internal const string OpenTelemetryErrorMsg = "exception.message";
        internal const string OpenTelemetryErrorStack = "exception.stacktrace";

        internal static void UpdateSpanFromActivity<TInner>(TInner activity, Span span)
            where TInner : IActivity
        {
            var activity5 = activity as IActivity5;

            AgentConvertSpan(activity, span);
        }

        // See trace agent func convertSpan: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L459
        private static void AgentConvertSpan<TInner>(TInner activity, Span span)
            where TInner : IActivity
        {
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
                span.SetTag("otel.trace_id", w3cActivity.TraceId);

                // Marshall events here using tag "events"
                // span.SetTag("events", eventsArray);
            }

            // Fixup "version" tag
            if (Tracer.Instance.Settings.ServiceVersionInternal is null
                && span.GetTag("service.version") is { Length: > 1 } otelServiceVersion)
            {
                span.SetTag(Tags.Version, otelServiceVersion);
            }

            // Copy over tags from Activity to the Datadog Span
            // Starting with .NET 5, Activity can hold tags whose value have type object?
            // For runtimes older than .NET 5, Activity can only hold tags whose values have type string
            if (activity5 is not null)
            {
                foreach (var activityTag in activity5.TagObjects)
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

            // Additional Datadog policy: Set tag "span.kind"
            // Since the ActivityKind can only be one of a fixed set of values, always set the tag as prescribed by Datadog practices
            // even though the tag is not present on normal OTLP spans
            if (activity5 is not null)
            {
                span.SetTag(Tags.SpanKind, GetSpanKind(activity5.Kind));
            }

            // TODO should we rename this flag to something that doesn't have Legacy in it?
            // Later: Support config 'span_name_as_resource_name'
            // Later: Support config 'span_name_remappings'
            if (Tracer.Instance.Settings.OpenTelemetryLegacyOperationNameEnabled && activity5 is not null)
            {
                span.OperationName = activity5.Source.Name switch
                {
                    string libName when !string.IsNullOrEmpty(libName) => $"{libName}.{GetSpanKind(activity5.Kind)}",
                    _ => $"opentelemetry.{GetSpanKind(activity5.Kind)}",
                };
            }
            else
            {
                ActivityOperationNameMapper.MapToOperationName(span);
            }

            // TODO: Add container tags from attributes if the tag isn't already in the span

            // Fixup "env" tag
            if (span.Context.TraceContext?.Environment is null
                && span.GetTag("deployment.environment") is { Length: > 0 } otelServiceEnv)
            {
                span.SetTag(Tags.Env, otelServiceEnv);
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
                    span.SetTag("otel.library.name", activity5.Source.Name);
                }

                if (!string.IsNullOrEmpty(activity5.Source.Version))
                {
                    span.SetTag("otel.library.version", activity5.Source.Version);
                }
            }

            // Set OTEL status code and OTEL status description
            if (span.GetTag("otel.status_code") is null)
            {
                if (activity6 is not null)
                {
                    switch (activity6.Status)
                    {
                        case ActivityStatusCode.Unset:
                            span.SetTag("otel.status_code", "STATUS_CODE_UNSET");
                            break;
                        case ActivityStatusCode.Ok:
                            span.SetTag("otel.status_code", "STATUS_CODE_OK");
                            break;
                        case ActivityStatusCode.Error:
                            span.SetTag("otel.status_code", "STATUS_CODE_ERROR");
                            break;
                        default:
                            span.SetTag("otel.status_code", "STATUS_CODE_UNSET");
                            break;
                    }
                }
                else
                {
                    span.SetTag("otel.status_code", "STATUS_CODE_UNSET");
                }
            }

            // Map the OTEL status to error tags
            AgentStatus2Error(activity, span);

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

        internal static void SetTagObject(Span span, string key, object? value)
        {
            if (value is null)
            {
                span.SetTag(key, null);
                return;
            }

            switch (value)
            {
                case char c: // TODO: Can't get here from OTEL API, test with Activity API
                    AgentSetOtlpTag(span, key, c.ToString());
                    break;
                case string s:
                    AgentSetOtlpTag(span, key, s);
                    break;
                case bool b:
                    AgentSetOtlpTag(span, key, b ? "true" : "false");
                    break;
                case byte b: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, b);
                    break;
                case sbyte sb: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, sb);
                    break;
                case short sh: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, sh);
                    break;
                case ushort us: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, us);
                    break;
                case int i: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, i);
                    break;
                case uint ui: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, ui);
                    break;
                case long l: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, l);
                    break;
                case ulong ul: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, ul);
                    break;
                case float f: // TODO: Can't get here from OTEL API, test with Activity API
                    span.SetMetric(key, f);
                    break;
                case double d:
                    span.SetMetric(key, d);
                    break;
                case IEnumerable enumerable:
                    AgentSetOtlpTag(span, key, JsonConvert.SerializeObject(enumerable));
                    break;
                default:
                    AgentSetOtlpTag(span, key, value.ToString());
                    break;
            }
        }

        // See trace agent func setMetaOTLP: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L424
        internal static void AgentSetOtlpTag(Span span, string key, string? value)
        {
            switch (key)
            {
                case "operation.name":
                    span.OperationName = value;
                    break;
                case "service.name":
                    span.ServiceName = value;
                    break;
                case "resource.name":
                    span.ResourceName = value;
                    break;
                case "span.type":
                    span.Type = value;
                    break;
                case "analytics.event":
                    if (GoStrConvParseBool(value) is bool b)
                    {
                        span.SetMetric(Tags.Analytics, b ? 1 : 0);
                    }

                    break;
                case "otel.status_code":
                    var newStatusCodeString = value switch
                    {
                        null => "STATUS_CODE_UNSET",
                        "ERROR" => "STATUS_CODE_ERROR",
                        "UNSET" => "STATUS_CODE_UNSET",
                        "OK" => "STATUS_CODE_OK",
                        string s => s,
                    };
                    span.SetTag(key, newStatusCodeString);
                    break;
                default:
                    span.SetTag(key, value);
                    break;
            }
        }

        // See trace agent func status2Error: https://github.com/DataDog/datadog-agent/blob/67c353cff1a6a275d7ce40059aad30fc6a3a0bc1/pkg/trace/api/otlp.go#L583
        internal static void AgentStatus2Error<TInner>(TInner activity, Span span)
            where TInner : IActivity
        {
            if (activity is IActivity6 { Status: ActivityStatusCode.Error } activity6)
            {
                span.Error = true;

                // First iterate through Activity events first and set error.msg, error.type, and error.stack
                ExtractExceptionAttributes(activity, span);

                if (span.GetTag(Tags.ErrorMsg) is null)
                {
                    span.SetTag(Tags.ErrorMsg, activity6.StatusDescription);
                }
            }
            else if (span.GetTag("otel.status_code") == "STATUS_CODE_ERROR")
            {
                span.Error = true;

                // First iterate through Activity events first and set error.msg, error.type, and error.stack
                ExtractExceptionAttributes(activity, span);

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
        }

        /// <summary>
        /// Iterates through <c>Activity.Events</c> looking for a <see cref="OpenTelemetryException"/> to copy over
        /// to the <see cref="Span.Tags"/> of <paramref name="span"/>.
        /// </summary>
        /// <param name="activity">The <see cref="IActivity"/> to check for the exception event data.</param>
        /// <param name="span">The <see cref="Span"/> to copy the exception event data to.</param>
        /// <typeparam name="TInner">
        /// The type of <c>Activity</c> - note that only <see cref="IActivity5"/> and up have support for events.
        /// </typeparam>
        /// <remarks>OpenTelemetry creates these attributes via it's <c>Activity.RecordException</c> function.</remarks>
        private static void ExtractExceptionAttributes<TInner>(TInner activity, Span span)
            where TInner : IActivity
        {
            // OpenTelemetry stores the exception attributes in Activity.Events
            // Activity.Events was only added in .NET 5+, which maps to our IActivity5 & IActivity6
            if (activity is not IActivity5 activity5)
            {
                return;
            }

            foreach (var activityEvent in activity5.Events)
            {
                if (!activityEvent.TryDuckCast<ActivityEvent>(out var duckEvent))
                {
                    continue;
                }

                if (duckEvent.Name != OpenTelemetryException)
                {
                    continue;
                }

                foreach (var tag in duckEvent.Tags)
                {
                    switch (tag.Key)
                    {
                        case OpenTelemetryErrorType:
                            SetTagObject(span, Tags.ErrorType, tag.Value);
                            break;

                        case OpenTelemetryErrorMsg:
                            SetTagObject(span, Tags.ErrorMsg, tag.Value);
                            break;

                        case OpenTelemetryErrorStack:
                            SetTagObject(span, Tags.ErrorStack, tag.Value);
                            break;
                    }
                }

                // we've found the exception attribute so we should be done here
                return;
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
    }
}
