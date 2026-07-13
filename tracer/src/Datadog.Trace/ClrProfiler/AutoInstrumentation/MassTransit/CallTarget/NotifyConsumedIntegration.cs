// <copyright file="NotifyConsumedIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.CallTarget
{
    /// <summary>
    /// MassTransit BaseReceiveContext.NotifyConsumed calltarget instrumentation.
    /// Success-path counterpart to <see cref="NotifyFaultedIntegration"/> — finishes a saga's pending
    /// process span (see MassTransitCommon.PendingSagaProcessScopes).
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "MassTransit",
        TypeName = "MassTransit.Context.BaseReceiveContext",
        MethodName = "NotifyConsumed",
        ReturnTypeName = "System.Threading.Tasks.Task",
        ParameterTypeNames = new[] { "MassTransit.ConsumeContext`1[!!0]", ClrNames.TimeSpan, ClrNames.String },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = MassTransitConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotifyConsumedIntegration
    {
        /// <summary>
        /// OnMethodBegin callback.
        /// </summary>
        /// <remarks>
        /// The signature here mirrors <c>BaseReceiveContext.NotifyConsumed(ConsumeContext&lt;T&gt;, TimeSpan, string)</c>
        /// because CallTarget binds by exact parameter shape. <paramref name="context"/> and
        /// <paramref name="consumerType"/> are unused; retained solely to match the instrumented signature.
        /// </remarks>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the ConsumeContext</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method — the ReceiveContext.</param>
        /// <param name="context">ConsumeContext instance (unused; required by signature).</param>
        /// <param name="duration">Duration of the operation (unused; required by signature).</param>
        /// <param name="consumerType">Consumer type name (unused; required by signature).</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context, TimeSpan duration, string consumerType)
        {
            if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.MassTransit))
            {
                return CallTargetState.GetDefault();
            }

            if (instance is not null && MassTransitCommon.PendingSagaProcessScopes.TryGetValue(instance, out var sagaScope))
            {
                MassTransitCommon.PendingSagaProcessScopes.Remove(instance);
                sagaScope.Span.Finish();
            }

            return CallTargetState.GetDefault();
        }
    }
}
