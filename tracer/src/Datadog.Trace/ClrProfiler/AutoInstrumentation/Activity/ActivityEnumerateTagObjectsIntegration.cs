// <copyright file="ActivityEnumerateTagObjectsIntegration.cs" company="Datadog">
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
    /// CallTarget instrumentation for <c>System.Diagnostics.Activity.EnumerateTagObjects()</c>, the
    /// allocation-free tag-enumeration API added in DiagnosticSource 9.0 (returns
    /// <c>Activity.Enumerator&lt;KeyValuePair&lt;string, object?&gt;&gt;</c>, a struct cursor over Activity's
    /// internal <c>DiagNode</c> chain). ASP.NET Core and HttpClient's OTel instrumentation prefer this over
    /// <c>TagObjects</c> on DS 9+ to avoid the enumerator-boxing allocation. Without interception it walks
    /// Activity's own, empty internal tag list.
    /// <para>
    /// The rewritten method's return type is fixed to that exact <c>Enumerator&lt;T&gt;</c> struct, so
    /// <see cref="ActivityTagStorageEmitter{TTarget}"/> synthesises a real one over a <c>DiagNode</c> chain
    /// built from the Span's tags (via <see cref="ActivityTagProjection"/>) rather than returning a
    /// different shape. If that synthesis is unavailable (DiagnosticSource's internal layout doesn't match
    /// what we expect — see <see cref="ActivityTagStorageEmitter{TTarget}.IsAvailable"/>), the method body
    /// is not skipped and behaviour degrades to today's (empty) result rather than throwing.
    /// </para>
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "EnumerateTagObjects",
        ReturnTypeName = "System.Diagnostics.Activity+Enumerator`1[System.Collections.Generic.KeyValuePair`2[System.String,System.Object]]",
        ParameterTypeNames = new string[0],
        MinimumVersion = "9.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityEnumerateTagObjectsIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin — skip the body only if we have a span attached AND the synthesis helper for this
        /// concrete Activity type is available; otherwise let Activity's own (empty) enumeration run.
        /// </summary>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            // Fast-path bail when interception isn't enabled — see ActivitySetTagIntegration for rationale.
            if (!Tracer.Instance.Settings.IsActivityInterceptionEnabled)
            {
                return CallTargetState.GetDefault();
            }

            if (!ActivityTagStorageEmitter<TTarget>.IsAvailable)
            {
                return CallTargetState.GetDefault();
            }

            var scope = ActivityCustomPropertyAccessor<TTarget>.GetScope(instance);
            if (scope is not null)
            {
                return new CallTargetState(scope, null, skipMethodBody: true);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd — project the Span's tags into a <c>DiagNode</c> chain and hand back a real
        /// <c>Enumerator&lt;T&gt;</c> over it.
        /// </summary>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (state.GetSkipMethodBody())
            {
                var span = state.Scope?.Span;
                if (span is not null)
                {
                    var tags = ActivityTagProjection.ProjectToObjectList(span);
                    if (ActivityTagStorageEmitter<TTarget>.TryEnumerate<TReturn>(tags, out var enumerator))
                    {
                        return new CallTargetReturn<TReturn>(enumerator);
                    }
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
