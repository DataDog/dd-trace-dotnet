// <copyright file="NotifyFaultedExceptionIntegration.cs" company="Datadog">
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
    /// MassTransit BaseReceiveContext.NotifyFaulted(Exception) calltarget instrumentation.
    /// This separate overload is required because CallTarget binds to exact method signatures.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "MassTransit",
        TypeName = "MassTransit.Context.BaseReceiveContext",
        MethodName = "NotifyFaulted",
        ReturnTypeName = "System.Threading.Tasks.Task",
        ParameterTypeNames = new[] { ClrNames.Exception },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = MassTransitConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotifyFaultedExceptionIntegration
    {
        /// <summary>
        /// OnMethodBegin callback.
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">The exception that occurred</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, Exception exception)
            => NotifyFaultedIntegration.Common.Handle(instance, exception);
    }
}
