// <copyright file="ActivityGetTagItemIntegration.cs" company="Datadog">
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
    /// CallTarget instrumentation for <c>System.Diagnostics.Activity.GetTagItem(string)</c>.
    /// Available on DiagnosticSource 7.0+ (.NET 7+). ASP.NET Core and HttpClient's OTel instrumentation
    /// use this for enrichment, so without interception it always reads Activity's empty internal tag
    /// list and returns null. Served back from the Span via <see cref="ActivityTagProjection.GetTagItem"/>.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "GetTagItem",
        ReturnTypeName = ClrNames.Object,
        ParameterTypeNames = new[] { ClrNames.String },
        MinimumVersion = "7.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityGetTagItemIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin — skip the body if we have a span attached, stashing the key for OnMethodEnd.
        /// </summary>
        internal static CallTargetState OnMethodBegin<TTarget, TKey>(TTarget instance, TKey key)
        {
            // Fast-path bail when interception isn't enabled — see ActivitySetTagIntegration for rationale.
            if (!Tracer.Instance.Settings.IsActivityInterceptionEnabled)
            {
                return CallTargetState.GetDefault();
            }

            var scope = ActivityCustomPropertyAccessor<TTarget>.GetScope(instance);
            if (scope is not null)
            {
                var keyStr = (string)(object)key!;
                return new CallTargetState(scope, state: keyStr, skipMethodBody: true);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd — look the key up on the Span via the shared keyed-lookup helper.
        /// </summary>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (state.GetSkipMethodBody())
            {
                var span = state.Scope?.Span;
                var key = state.State as string;
                if (span is not null && key is not null)
                {
                    var value = ActivityTagProjection.GetTagItem(span, key);
                    return new CallTargetReturn<TReturn>((TReturn)(object)value!);
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
