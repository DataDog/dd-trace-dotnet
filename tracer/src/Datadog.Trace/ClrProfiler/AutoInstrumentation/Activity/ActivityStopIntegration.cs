// <copyright file="ActivityStopIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// CallTarget instrumentation for <c>System.Diagnostics.Activity.Stop()</c>.
    /// Retrieves the <see cref="Scope"/> stored on the Activity and finishes the associated Span.
    /// The Stop() body is NOT skipped — it must run to set Activity.Duration and restore Activity.Current.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "Stop",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new string[0],
        MinimumVersion = "6.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityStopIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityStopIntegration));

        /// <summary>
        /// OnMethodBegin callback — retrieve the Scope from the Activity's custom property before Stop() runs.
        /// Activity.Duration is not yet set here, but we save the scope in state for OnMethodEnd.
        /// </summary>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (!Tracer.Instance.Settings.IsActivityInterceptionEnabled)
            {
                return CallTargetState.GetDefault();
            }

            // Retrieve scope early; save in state so OnMethodEnd can access it after Stop() sets Duration.
            // Also retrieve the initial operation name saved at Start() time so OnMethodEnd can detect
            // whether the user explicitly changed it via an "operation.name" tag.
            var scope = ActivityCustomPropertyAccessor<TTarget>.GetScope(instance);
            var initialOpName = ActivityCustomPropertyAccessor<TTarget>.GetInitialOperationName(instance);
            return new CallTargetState(scope, state: initialOpName);
        }

        /// <summary>
        /// OnMethodEnd callback — called after Activity.Stop() has run and Duration is set.
        /// Finalizes the span using the timing from the Activity.
        /// </summary>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        {
            try
            {
                var scope = state.Scope;
                if (scope is null)
                {
                    return CallTargetReturn.GetDefault();
                }

                if (!instance.TryDuckCast<IActivity5>(out var activity5))
                {
                    scope.Span.Finish();
                    scope.Close();
                    return CallTargetReturn.GetDefault();
                }

                var span = scope.Span;

                // Handle late-set fields: Links and Events (added by OTel SDK at stop time)
                OtlpHelpers.ExtractLinksAndEventsFromActivity(activity5, span);

                // Status fallback: if SetStatus was not intercepted (e.g., old DiagnosticSource without Status).
                // Must duck cast to IActivity6 separately; an IActivity5 proxy does not automatically implement IActivity6.
                if (instance.TryDuckCast<IActivity6>(out var activity6))
                {
                    ApplyStatusFallback(activity6, span);
                }

                // Resource name fallback: if set_DisplayName was not intercepted
                if (span.ResourceName is null && activity5.DisplayName is { Length: > 0 } displayName)
                {
                    span.ResourceName = displayName;
                }

                // Update service name if not yet set
                if (span.ServiceName is null)
                {
                    span.SetService(
                        span.GetTag("peer.service") switch
                        {
                            string peerService when !string.IsNullOrEmpty(peerService) => peerService,
                            _ => "OTLPResourceNoServiceName",
                        },
                        source: null);
                }

                if (span.Tags is OpenTelemetryTags otelTags)
                {
                    // Derive operation name from semantic conventions if the user didn't explicitly set it
                    // via an "operation.name" tag (which sets span.OperationName via AgentSetOtlpTag).
                    // We detect explicit override by comparing against the initial name saved in OnMethodBegin.
                    var initialOpName = state.State as string;
                    if (span.OperationName == initialOpName)
                    {
                        span.OperationName = OperationNameMapper.GetOperationName(otelTags);
                    }

                    // Derive span type from ActivityKind if not already set
                    if (string.IsNullOrWhiteSpace(span.Type))
                    {
                        span.Type = OtlpHelpers.AgentSpanKind2Type(activity5.Kind, span);
                    }
                }

                // Fixup TraceContext environment and service version from OTel tags
                var traceContext = span.Context.TraceContext;
                if (traceContext is not null)
                {
                    if (traceContext.Environment is null
                     && span.GetTag("deployment.environment") is { Length: > 0 } otelEnv)
                    {
                        traceContext.Environment = otelEnv;
                    }

                    if (string.IsNullOrEmpty(traceContext.ServiceVersion)
                     && span.GetTag("service.version") is { Length: > 0 } otelVersion)
                    {
                        traceContext.ServiceVersion = otelVersion;
                    }
                }

                // Finish with the precise timing from Activity
                var finishTime = activity5.StartTimeUtc.Add(activity5.Duration);
                span.Finish(finishTime);
                scope.Close();

                // Clean up the custom property to avoid memory leaks
                ActivityCustomPropertyAccessor<TTarget>.SetScope(instance, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ActivityStopIntegration.OnMethodEnd");
            }

            return CallTargetReturn.GetDefault();
        }

        private static void ApplyStatusFallback(IActivity6 activity6, Span span)
        {
            if (span.Tags is not OpenTelemetryTags tags)
            {
                return;
            }

            // Only apply if status was not already set by ActivitySetStatusIntegration
            if (tags.OtelStatusCode is null)
            {
                tags.OtelStatusCode = activity6.Status switch
                {
                    ActivityStatusCode.Unset => "STATUS_CODE_UNSET",
                    ActivityStatusCode.Ok => "STATUS_CODE_OK",
                    ActivityStatusCode.Error => "STATUS_CODE_ERROR",
                    _ => "STATUS_CODE_UNSET"
                };

                if (activity6.Status == ActivityStatusCode.Error)
                {
                    span.Error = true;
                    if (activity6.StatusDescription is { Length: > 0 } desc
                     && span.GetTag(Tags.ErrorMsg) is null)
                    {
                        span.SetTag(Tags.ErrorMsg, desc);
                    }
                }
            }
        }
    }
}
