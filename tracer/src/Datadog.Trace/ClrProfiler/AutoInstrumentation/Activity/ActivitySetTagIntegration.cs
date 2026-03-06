// <copyright file="ActivitySetTagIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.Activity;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// CallTarget instrumentation for <c>System.Diagnostics.Activity.SetTag(string, object?)</c>.
    /// Available on DiagnosticSource 5.0+ (.NET 5+). Unlike AddTag, SetTag replaces an existing tag.
    /// When the feature flag is enabled and the Activity is tracked, bypasses Activity's internal
    /// tag storage and writes the tag directly to the Datadog Span instead.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "SetTag",
        ReturnTypeName = "System.Diagnostics.Activity",
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.Object },
        MinimumVersion = "6.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivitySetTagIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin — intercept the tag and optionally skip Activity's internal storage.
        /// </summary>
        internal static CallTargetState OnMethodBegin<TTarget, TKey, TValue>(TTarget instance, TKey key, TValue value)
        {
            var scope = ActivityCustomPropertyAccessor<TTarget>.GetScope(instance);
            if (scope is not null)
            {
                var keyStr = (string)(object)key!;
                OtlpHelpers.SetTagObject(scope.Span, keyStr, (object?)value);
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
    }
}
