// <copyright file="ActivityTagObjectsGetterIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// CallTarget instrumentation for the <c>System.Diagnostics.Activity.TagObjects</c> property getter
    /// (returns <c>IEnumerable&lt;KeyValuePair&lt;string, object?&gt;&gt;</c>).
    /// Available on DiagnosticSource 5.0+ (.NET 5+).
    /// When the feature flag is enabled and the Activity is tracked, tag setters (AddTag, SetTag) skip
    /// Activity's internal tag storage and write to the Span instead. This getter reads tags back from
    /// the Span so that Activity.TagObjects returns the correct values.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "get_TagObjects",
        ReturnTypeName = "System.Collections.Generic.IEnumerable`1[System.Collections.Generic.KeyValuePair`2[System.String,System.Object]]",
        ParameterTypeNames = new string[0],
        MinimumVersion = "6.0.0",
        MaximumVersion = SupportedVersions.LatestDotNet,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityTagObjectsGetterIntegration
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
        /// OnMethodEnd — build an enumerable of object-valued tags from the Span's tag storage.
        /// Tags are stored internally as strings; they are boxed to object here to match the
        /// Activity.TagObjects contract. Numeric values that were originally set via SetMetric
        /// are not reflected here (they are not retrievable as tags from the Span).
        /// </summary>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (state.GetSkipMethodBody())
            {
                var span = state.Scope?.Span;
                if (span is not null)
                {
                    var list = new List<KeyValuePair<string, object?>>();
                    var processor = new ObjectTagListBuilder(list);
                    span.Tags.EnumerateTags(ref processor);
                    return new CallTargetReturn<TReturn>((TReturn)(object)list);
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        private struct ObjectTagListBuilder : IItemProcessor<string>
        {
            private readonly List<KeyValuePair<string, object?>> _list;

            public ObjectTagListBuilder(List<KeyValuePair<string, object?>> list) => _list = list;

            public void Process(TagItem<string> item) => _list.Add(new KeyValuePair<string, object?>(item.Key, item.Value));
        }
    }
}
