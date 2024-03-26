// <copyright file="LoggerImplWriteIntegrationV4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection
{
    /// <summary>
    /// LoggerImpl.Write calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "NLog",
        TypeName = "NLog.LoggerImpl",
        MethodName = "Write",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.Type, "NLog.Internal.TargetWithFilterChain", "NLog.LogEventInfo", "NLog.LogFactory" },
        MinimumVersion = "1.0.0.505",
        MaximumVersion = "4.*.*",
        IntegrationName = "NLog")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggerImplWriteIntegrationV4
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TTargets">The type of the TargetWithFilterChain </typeparam>
        /// <typeparam name="TLogEventInfo">The type of the LogEventInfo</typeparam>
        /// <typeparam name="TLogFactory">The type of the LogFactory</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="loggerType">The instance of the logger type</param>
        /// <param name="targetsForLevel">The instance of the targets for the level</param>
        /// <param name="logEvent">The logging event instance</param>
        /// <param name="factory">The LogFactory instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TTargets, TLogEventInfo, TLogFactory>(TTarget instance, Type loggerType, TTargets targetsForLevel, TLogEventInfo logEvent, TLogFactory factory)
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.LogsInjectionEnabledInternal)
            {
                if (DiagnosticContextHelper.Cache<TTarget>.Mdlc is { } mdlc)
                {
                    var state = DiagnosticContextHelper.SetMdlcState(mdlc, tracer);
                    return new CallTargetState(scope: null, state);
                }

                if (DiagnosticContextHelper.Cache<TTarget>.Mdc is { } mdc)
                {
                    var removeSpanId = DiagnosticContextHelper.SetMdcState(mdc, tracer);
                    return new CallTargetState(scope: null, removeSpanId);
                }
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            if (state.State is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (state.State is bool removeTraceIds && DiagnosticContextHelper.Cache<TTarget>.Mdc is { } mdc)
            {
                DiagnosticContextHelper.RemoveMdcState(mdc, removeTraceIds);
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
