// <copyright file="ActivityStatusDescriptionGetterIntegration.cs" company="Datadog">
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
    /// CallTarget instrumentation for the <c>System.Diagnostics.Activity.StatusDescription</c> property getter.
    /// Available on DiagnosticSource 6.0+ (.NET 6+).
    /// When the feature flag is enabled and the Activity is tracked, <see cref="ActivitySetStatusIntegration"/>
    /// skips Activity's internal status fields; this getter reads the description back from the linked Span
    /// so the observable value stays consistent.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "get_StatusDescription",
        ReturnTypeName = ClrNames.String,
        ParameterTypeNames = new string[0],
        MinimumVersion = "6.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityStatusDescriptionGetterIntegration
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
        /// OnMethodEnd — return the status description from the Span's "otel.status_description" tag.
        /// </summary>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (state.GetSkipMethodBody())
            {
                var span = state.Scope?.Span;
                if (span is not null)
                {
                    var description = span.GetTag("otel.status_description");
                    return new CallTargetReturn<TReturn>((TReturn)(object?)description!);
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
