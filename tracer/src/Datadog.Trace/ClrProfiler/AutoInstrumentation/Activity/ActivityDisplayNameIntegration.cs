// <copyright file="ActivityDisplayNameIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// CallTarget instrumentation for the <c>System.Diagnostics.Activity.DisplayName</c> property setter.
    /// When the feature flag is enabled and the Activity is tracked, bypasses Activity's internal
    /// DisplayName field and sets <see cref="Span.ResourceName"/> on the Datadog Span directly.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "set_DisplayName",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.String },
        MinimumVersion = "6.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityDisplayNameIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin — set the span's ResourceName and skip Activity's internal DisplayName field.
        /// </summary>
        internal static CallTargetState OnMethodBegin<TTarget, TValue>(TTarget instance, TValue value)
        {
            var scope = ActivityCustomPropertyAccessor<TTarget>.GetScope(instance);
            if (scope is not null)
            {
                scope.Span.ResourceName = (string?)(object?)value;
                return new CallTargetState(null, null, skipMethodBody: true);
            }

            return CallTargetState.GetDefault();
        }
    }
}
