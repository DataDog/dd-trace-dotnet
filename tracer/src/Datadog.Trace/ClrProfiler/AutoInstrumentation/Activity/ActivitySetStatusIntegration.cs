// <copyright file="ActivitySetStatusIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// CallTarget instrumentation for <c>System.Diagnostics.Activity.SetStatus(ActivityStatusCode, string?)</c>.
    /// Available on DiagnosticSource 6.0+ (.NET 6+).
    /// When the feature flag is enabled and the Activity is tracked, maps the OTel status to Datadog
    /// error tags on the Span and bypasses Activity's internal status field.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "SetStatus",
        ReturnTypeName = "System.Diagnostics.Activity",
        ParameterTypeNames = new[] { "System.Diagnostics.ActivityStatusCode", ClrNames.String },
        MinimumVersion = "6.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivitySetStatusIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin — apply the OTel status to the Span and skip Activity's internal field.
        /// </summary>
        /// <typeparam name="TTarget">Activity type</typeparam>
        /// <typeparam name="TStatusCode">ActivityStatusCode enum type</typeparam>
        /// <typeparam name="TDescription">string type</typeparam>
        internal static CallTargetState OnMethodBegin<TTarget, TStatusCode, TDescription>(TTarget instance, TStatusCode statusCode, TDescription description)
        {
            var scope = ActivityCustomPropertyAccessor<TTarget>.GetScope(instance);
            if (scope is not null)
            {
                // Convert.ToInt32 correctly handles enum→int conversion even when TStatusCode
                // is a foreign enum type (System.Diagnostics.ActivityStatusCode from the target assembly).
                // Direct cast like (int)(object)statusCode would throw InvalidCastException for enums.
                ApplyStatus(scope.Span, System.Convert.ToInt32((object)statusCode!), (string?)(object?)description);
                return new CallTargetState(null, null, skipMethodBody: true);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd — restore the instance as return value when the method body was skipped.
        /// </summary>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (state.GetSkipMethodBody())
            {
                return new CallTargetReturn<TReturn>((TReturn)(object)instance!);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        private static void ApplyStatus(Span span, int statusCodeInt, string? description)
        {
            if (span.Tags is not OpenTelemetryTags tags)
            {
                return;
            }

            // Map ActivityStatusCode enum value to OTel status string
            // ActivityStatusCode: Unset = 0, Ok = 1, Error = 2
            switch (statusCodeInt)
            {
                case 0: // Unset
                    tags.OtelStatusCode = "STATUS_CODE_UNSET";
                    break;
                case 1: // Ok
                    tags.OtelStatusCode = "STATUS_CODE_OK";
                    break;
                case 2: // Error
                    tags.OtelStatusCode = "STATUS_CODE_ERROR";
                    span.Error = true;
                    if (!string.IsNullOrEmpty(description) && span.GetTag(Tags.ErrorMsg) is null)
                    {
                        span.SetTag(Tags.ErrorMsg, description);
                    }

                    // Also set status description tag
                    if (!string.IsNullOrEmpty(description))
                    {
                        span.SetTag("otel.status_description", description);
                    }

                    break;
                default:
                    tags.OtelStatusCode = "STATUS_CODE_UNSET";
                    break;
            }
        }
    }
}
