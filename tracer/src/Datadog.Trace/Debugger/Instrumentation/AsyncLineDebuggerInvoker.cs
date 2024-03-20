// <copyright file="AsyncLineDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// LineDebuggerInvoker Invoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AsyncLineDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncLineDebuggerInvoker));

        private static AsyncLineDebuggerState CreateInvalidatedAsyncLineDebuggerState()
        {
            var defaultState = AsyncLineDebuggerState.GetDefault();
            defaultState.IsActive = false;
            return defaultState;
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref AsyncLineDebuggerState state)
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

                var captureInfo = new CaptureInfo<TLocal>(state.MethodMetadataIndex, value: local, methodState: MethodState.LogLocal, name: localName, memberKind: ScopeMemberKind.Local);
                var probeData = state.ProbeData;

                if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData))
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
        public static void LogException(Exception exception, ref AsyncLineDebuggerState state)
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
        /// <param name="probeMetadataIndex">The index used to lookup for the <see cref="ProbeData"/></param>
        /// <param name="instance">Instance value</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="methodMetadataIndex">The index used to lookup the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="lineNumber">The line instrumented</param>
        /// <param name="probeFilePath">The path to the file of the probe</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncLineDebuggerState BeginLine<TTarget>(string probeId, int probeMetadataIndex, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex, int lineNumber, string probeFilePath)
        {
            try
            {
                if (instance == null)
                {
                    // Should not happen, but placing it here for a safeguard.
                    // We may load `instance` (aka `this`) as null when we deal with complex stack-allocated values types that are generic (either open/close generics).
                    // In the native side we already have a safeguard that will fail to instrument a method if we deal with such situations but I also placed a safeguard in the managed side just in case.
                    return CreateInvalidatedAsyncLineDebuggerState();
                }

                // Assess if we have metadata associated with the given index
                if (!MethodMetadataCollection.Instance.IndexExists(methodMetadataIndex))
                {
                    // State machine is null when we run in Optimized code and the original async method was generic,
                    // in which case the state machine is a generic value type.

                    var stateMachineType = instance.GetType();
                    var kickoffInfo = AsyncHelper.GetAsyncKickoffMethodInfo(instance, stateMachineType);
                    if (kickoffInfo.KickoffParentObject == null && kickoffInfo.KickoffMethod.IsStatic == false)
                    {
                        Log.Warning(nameof(BeginLine) + ": hoisted 'this' was not found. {KickoffParentType}.{KickoffMethod}", kickoffInfo.KickoffParentType.Name, kickoffInfo.KickoffMethod.Name);
                    }

                    if (!MethodMetadataCollection.Instance.TryCreateAsyncMethodMetadataIfNotExists(instance, methodMetadataIndex, in methodHandle, in typeHandle, kickoffInfo))
                    {
                        Log.Warning("([Async]BeginLine: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {Type}, instance type name = {Name}, methodMetadataId = {MethodMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance.GetType().Name, methodMetadataIndex, probeId });
                        return CreateInvalidatedAsyncLineDebuggerState();
                    }
                }

                ref var probeData = ref ProbeDataCollection.Instance.TryCreateProbeDataIfNotExists(probeMetadataIndex, probeId);
                if (probeData.IsEmpty())
                {
                    Log.Warning("[Async]BeginLine: Failed to receive the ProbeData associated with the executing probe. type = {Type}, instance type name = {Name}, probeMetadataIndex = {ProbeMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, probeMetadataIndex, probeId });
                    return CreateInvalidatedAsyncLineDebuggerState();
                }

                if (!probeData.Processor.ShouldProcess(in probeData))
                {
                    Log.Warning("[Async]BeginLine: Skipping the instrumentation. type = {Type}, instance type name = {Name}, probeMetadataIndex = {ProbeMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, probeMetadataIndex, probeId });
                    return CreateInvalidatedAsyncLineDebuggerState();
                }

                var kickoffParentObject = AsyncHelper.GetAsyncKickoffThisObject(instance);
                var state = new AsyncLineDebuggerState(probeId, scope: default, methodMetadataIndex, ref probeData, lineNumber, probeFilePath, instance, kickoffParentObject);
                var asyncInfo = new AsyncCaptureInfo(state.MoveNextInvocationTarget, state.KickoffInvocationTarget, state.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: state.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: state.MethodMetadataInfo.AsyncMethodHoistedArguments);
                var captureInfo = new CaptureInfo<Type>(state.MethodMetadataIndex, value: null, type: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.BeginLineAsync, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length, lineCaptureInfo: new LineCaptureInfo(lineNumber, probeFilePath), asyncCaptureInfo: asyncInfo);

                if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData))
                {
                    state.IsActive = false;
                }

                return state;
            }
            catch (Exception e)
            {
                var invalidState = new AsyncLineDebuggerState();
                LogException(e, ref invalidState);
                return invalidState;
            }
        }

        /// <summary>
        /// End Line
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndLine(ref AsyncLineDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    return;
                }

                var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                           state.MethodMetadataInfo.AsyncMethodHoistedArguments.Length > 0 ||
                                           state.KickoffInvocationTarget != null;

                state.HasLocalsOrReturnValue = false;
                var asyncCaptureInfo = new AsyncCaptureInfo(state.MoveNextInvocationTarget, state.KickoffInvocationTarget, state.MethodMetadataInfo.KickoffInvocationTargetType, kickoffMethod: state.MethodMetadataInfo.KickoffMethod, hoistedArgs: state.MethodMetadataInfo.AsyncMethodHoistedArguments, hoistedLocals: state.MethodMetadataInfo.AsyncMethodHoistedLocals);
                var captureInfo = new CaptureInfo<object>(state.MethodMetadataIndex, value: state.KickoffInvocationTarget, type: state.MethodMetadataInfo.KickoffInvocationTargetType, name: "this", memberKind: ScopeMemberKind.This, methodState: MethodState.EndLineAsync, hasLocalOrArgument: hasArgumentsOrLocals, asyncCaptureInfo: asyncCaptureInfo, lineCaptureInfo: new LineCaptureInfo(state.LineNumber, state.ProbeFilePath));
                var probeData = state.ProbeData;
                state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData);
            }
            catch (Exception e)
            {
                LogException(e, ref state);
            }
        }
    }
}
