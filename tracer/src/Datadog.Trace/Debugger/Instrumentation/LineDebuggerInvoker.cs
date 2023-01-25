// <copyright file="LineDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// LineDebuggerInvoker Invoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class LineDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LineDebuggerInvoker));

        private static LineDebuggerState CreateInvalidatedLineDebuggerState()
        {
            var defaultState = LineDebuggerState.GetDefault();
            defaultState.IsActive = false;
            return defaultState;
        }

        /// <summary>
        /// Logs the given <paramref name="arg"/> ByRef.
        /// </summary>
        /// <typeparam name="TArg">Type of argument.</typeparam>
        /// <param name="arg">The argument to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(ref TArg arg, int index, ref LineDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    return;
                }

                var paramName = state.MethodMetadataInfo.ParameterNames[index];
                var captureInfo = new CaptureInfo<TArg>(value: arg, type: typeof(TArg), methodState: MethodState.LogArg, name: paramName, memberKind: ScopeMemberKind.Argument);
                if (!ProbeExpressionsProcessor.Instance.Process(state.ProbeId, ref captureInfo, state.SnapshotCreator))
                {
                    state.IsActive = false;
                }

                state.HasLocalsOrReturnValue = false;
            }
            catch (Exception e)
            {
                LogException(e, ref state);
            }
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref LineDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    return;
                }

                var localVariableNames = state.MethodMetadataInfo.LocalVariableNames;
                if (!MethodDebuggerInvoker.TryGetLocalName(index, localVariableNames, out var localName))
                {
                    return;
                }

                var captureInfo = new CaptureInfo<TLocal>(value: local, type: typeof(TLocal), methodState: MethodState.LogLocal, name: localName, memberKind: ScopeMemberKind.Local);
                if (!ProbeExpressionsProcessor.Instance.Process(state.ProbeId, ref captureInfo, state.SnapshotCreator))
                {
                    state.IsActive = false;
                }

                state.HasLocalsOrReturnValue = true;
            }
            catch (Exception e)
            {
                LogException(e, ref state);
            }
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref LineDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    // Already encountered `LogException`
                    return;
                }

                Log.Warning(exception, "Error caused by our instrumentation");
                state.IsActive = false;
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Gets the default value of a type
        /// </summary>
        /// <typeparam name="T">Type to get the default value</typeparam>
        /// <returns>Default value of T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetDefaultValue<T>() => default;

        /// <summary>
        /// Begin Line Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="instance">Instance value</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="lineNumber">The line instrumented</param>
        /// <param name="probeFilePath">The path to the file of the probe</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LineDebuggerState BeginLine<TTarget>(string probeId, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex, int lineNumber, string probeFilePath)
        {
            try
            {
                if (!MethodMetadataProvider.TryCreateNonAsyncMethodMetadataIfNotExists(methodMetadataIndex, in methodHandle, in typeHandle))
                {
                    Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadaId = {methodMetadataIndex}");
                    return CreateInvalidatedLineDebuggerState();
                }

                var state = new LineDebuggerState(probeId, scope: default, DateTimeOffset.UtcNow, methodMetadataIndex, lineNumber, probeFilePath, instance);

                if (!state.SnapshotCreator.ProbeHasCondition &&
                    !ProbeRateLimiter.Instance.Sample(probeId))
                {
                    return CreateInvalidatedLineDebuggerState();
                }

                var captureInfo = new CaptureInfo<Type>(invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.BeginLine, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length, lineCaptureInfo: new LineCaptureInfo(lineNumber, probeFilePath));
                if (!ProbeExpressionsProcessor.Instance.Process(probeId, ref captureInfo, state.SnapshotCreator))
                {
                    state.IsActive = false;
                }

                return state;
            }
            catch (Exception e)
            {
                var invalidState = new LineDebuggerState();
                LogException(e, ref invalidState);
                return invalidState;
            }
        }

        /// <summary>
        /// End Line
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndLine(ref LineDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    return;
                }

                var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                           state.MethodMetadataInfo.ParameterNames.Length > 0 ||
                                           !state.MethodMetadataInfo.Method.IsStatic;

                var captureInfo = new CaptureInfo<object>(value: state.InvocationTarget, type: state.MethodMetadataInfo.DeclaringType, invocationTargetType: state.MethodMetadataInfo.DeclaringType, memberKind: ScopeMemberKind.This, methodState: MethodState.EndLine, hasLocalOrArgument: hasArgumentsOrLocals, method: state.MethodMetadataInfo.Method, lineCaptureInfo: new LineCaptureInfo(state.LineNumber, state.ProbeFilePath));
                ProbeExpressionsProcessor.Instance.Process(state.ProbeId, ref captureInfo, state.SnapshotCreator);

                state.HasLocalsOrReturnValue = false;
            }
            catch (Exception e)
            {
                LogException(e, ref state);
            }
        }
    }
}
