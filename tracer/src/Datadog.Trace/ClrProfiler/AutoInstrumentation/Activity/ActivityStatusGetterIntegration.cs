// <copyright file="ActivityStatusGetterIntegration.cs" company="Datadog">
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
    /// CallTarget instrumentation for the <c>System.Diagnostics.Activity.Status</c> property getter.
    /// Available on DiagnosticSource 6.0+ (.NET 6+).
    /// When the feature flag is enabled and the Activity is tracked, <see cref="ActivitySetStatusIntegration"/>
    /// skips Activity's internal Status field; this getter reads the status back from the linked Span's
    /// OtelStatusCode tag so the observable value stays consistent.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "get_Status",
        ReturnTypeName = "System.Diagnostics.ActivityStatusCode",
        ParameterTypeNames = new string[0],
        MinimumVersion = "6.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityStatusGetterIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin — skip the body if we have a span attached.
        /// </summary>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            var scope = ActivityCustomPropertyAccessor<TTarget>.GetScope(instance);
            if (scope is not null)
            {
                return new CallTargetState(scope, null, skipMethodBody: true);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd — return the ActivityStatusCode reconstructed from the Span's OtelStatusCode tag.
        /// ActivityStatusCode enum values: Unset = 0, Ok = 1, Error = 2.
        /// </summary>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (state.GetSkipMethodBody())
            {
                var span = state.Scope?.Span;
                if (span is not null)
                {
                    var statusCodeInt = (span.Tags is OpenTelemetryTags tags ? tags.OtelStatusCode : null) switch
                    {
                        "STATUS_CODE_OK" => 1,
                        "STATUS_CODE_ERROR" => 2,
                        _ => 0, // Unset
                    };

                    // Enum.ToObject boxes the int as the correct enum type (ActivityStatusCode),
                    // enabling a safe unbox cast to TReturn even for foreign enum types.
                    return new CallTargetReturn<TReturn>((TReturn)Enum.ToObject(typeof(TReturn), statusCodeInt));
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
