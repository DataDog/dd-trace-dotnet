// <copyright file="ActivityCreateAndStartIntegration.cs" company="Datadog">
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
    /// CallTarget instrumentation for the internal static method
    /// <c>System.Diagnostics.Activity.CreateAndStart(ActivitySource, string, ActivityKind, string, ActivityContext, IEnumerable&lt;KeyValuePair&lt;string,object?&gt;&gt;, IEnumerable&lt;ActivityLink&gt;, DateTimeOffset, ActivityTagsCollection, ActivitySamplingResult)</c>.
    /// Only present in DiagnosticSource 5.x — renamed to <c>Activity.Create(..., startIt)</c> in 6.0,
    /// where the body invokes the public <c>Activity.Start()</c> (which our
    /// <see cref="ActivityStartIntegration"/> already intercepts).
    ///
    /// On DS 5.x, <c>ActivitySource.StartActivity(...)</c> calls <c>Activity.CreateAndStart</c>,
    /// which performs the start work inline (generates W3C IDs, sets <c>StartTimeUtc</c>,
    /// makes the activity current, fires the listener start event) *without* invoking the public
    /// <c>Activity.Start()</c>. Without this intercept, the interception path cannot observe
    /// activity creation on DS 5.x and no Datadog spans are produced.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Diagnostics.DiagnosticSource",
        TypeName = "System.Diagnostics.Activity",
        MethodName = "CreateAndStart",
        ReturnTypeName = "System.Diagnostics.Activity",
        ParameterTypeNames = new[] { "System.Diagnostics.ActivitySource", ClrNames.String, "System.Diagnostics.ActivityKind", ClrNames.String, "System.Diagnostics.ActivityContext", "System.Collections.Generic.IEnumerable`1[System.Collections.Generic.KeyValuePair`2[System.String,System.Object]]", "System.Collections.Generic.IEnumerable`1[System.Diagnostics.ActivityLink]", "System.DateTimeOffset", "System.Diagnostics.ActivityTagsCollection", "System.Diagnostics.ActivitySamplingResult" },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.65535.65535",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ActivityCreateAndStartIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        internal static CallTargetState OnMethodBegin<TTarget, TSource, TKind, TContext, TTags, TLinks, TSamplerTags, TSamplingResult>(
            TTarget instance,
            TSource source,
            string name,
            TKind kind,
            string? parentId,
            TContext context,
            TTags tags,
            TLinks links,
            DateTimeOffset startTime,
            TSamplerTags samplerTags,
            TSamplingResult samplingResult)
        {
            return CallTargetState.GetDefault();
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            // CreateAndStart returns null when sampling rejects the activity. Nothing to wire.
            if (returnValue is null)
            {
                return new CallTargetReturn<TReturn>(returnValue);
            }

            ActivityStartIntegration.HandleStartedActivity(returnValue);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
