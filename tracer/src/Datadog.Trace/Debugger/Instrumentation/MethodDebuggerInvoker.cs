// <copyright file="MethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// MethodDebuggerInvoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class MethodDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodDebuggerInvoker));

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="probeMetadataIndex">The index used to lookup for the <see cref="ProbeData"/></param>
        /// <param name="instance">Instance value</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodDebuggerState BeginMethod_StartMarker<TTarget>(string probeId, int probeMetadataIndex, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex)
        {
            if (!MethodMetadataCollection.Instance.TryCreateNonAsyncMethodMetadataIfNotExists(methodMetadataIndex, in methodHandle, in typeHandle))
            {
                Log.Warning("BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {Type}, instance type name = {Name}, methodMetadaId = {MethodMetadataIndex}, probeId = {ProbeId}", typeof(TTarget), instance?.GetType().Name, methodMetadataIndex, probeId);
                return CreateInvalidatedDebuggerState();
            }

            ref var probeData = ref ProbeDataCollection.Instance.TryCreateProbeDataIfNotExists(probeMetadataIndex, probeId);
            if (probeData.IsEmpty())
            {
                Log.Warning("BeginMethod_StartMarker: Failed to receive the ProbeData associated with the executing probe. type = {Type}, instance type name = {Name}, probeMetadataIndex = {ProbeMetadataIndex}, probeId = {ProbeId}", typeof(TTarget), instance?.GetType().Name, probeMetadataIndex, probeId);
                return CreateInvalidatedDebuggerState();
            }

            var state = new MethodDebuggerState(probeId/* probeIds[i] */, scope: default, methodMetadataIndex, ref probeData, instance);

            if (!state.SnapshotCreator.ProbeHasCondition &&
                !state.ProbeData.Sampler.Sample())
            {
                return CreateInvalidatedDebuggerState();
            }

            var captureInfo = new CaptureInfo<Type>(value: null, type: state.MethodMetadataInfo.DeclaringType, invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.EntryStart, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length);

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator))
            {
                state.IsActive = false;
            }

            state.SnapshotCreator.StartSampling();
            return state;
        }

        /// <summary>
        /// Ends the markering of BeginMethod.
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginMethod_EndMarker(ref MethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            state.SnapshotCreator.StopSampling();
            var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                       state.MethodMetadataInfo.ParameterNames.Length > 0 ||
                                       !state.MethodMetadataInfo.Method.IsStatic;

            var captureInfo = new CaptureInfo<object>(value: state.InvocationTarget, type: state.MethodMetadataInfo.DeclaringType, invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.EntryEnd, hasLocalOrArgument: hasArgumentsOrLocals, memberKind: ScopeMemberKind.This);

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator))
            {
                state.IsActive = false;
            }

            state.HasLocalsOrReturnValue = false;
            state.SnapshotCreator.StartSampling();
        }

        /// <summary>
        /// Logs the given <paramref name="arg"/> ByRef.
        /// </summary>
        /// <typeparam name="TArg">Type of argument.</typeparam>
        /// <param name="arg">The argument to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(ref TArg arg, int index, ref MethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            state.SnapshotCreator.StopSampling();
            var paramName = state.MethodMetadataInfo.ParameterNames[index];
            var captureInfo = new CaptureInfo<TArg>(value: arg, methodState: MethodState.LogArg, name: paramName, memberKind: ScopeMemberKind.Argument);

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator))
            {
                state.IsActive = false;
            }

            state.HasLocalsOrReturnValue = false;
            state.SnapshotCreator.StartSampling();
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref MethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            state.SnapshotCreator.StopSampling();
            var localVariableNames = state.MethodMetadataInfo.LocalVariableNames;
            if (!TryGetLocalName(index, localVariableNames, out var localName))
            {
                state.SnapshotCreator.StartSampling();
                return;
            }

            var captureInfo = new CaptureInfo<TLocal>(value: local, methodState: MethodState.LogLocal, name: localName, memberKind: ScopeMemberKind.Local);

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator))
            {
                state.IsActive = false;
            }

            state.HasLocalsOrReturnValue = true;
            state.SnapshotCreator.StartSampling();
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        /// <returns>CallTarget return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref MethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return DebuggerReturn.GetDefault();
            }

            state.SnapshotCreator.StopSampling();
            state.MethodPhase = EvaluateAt.Exit;

            var captureInfo = new CaptureInfo<Exception>(value: exception, invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.ExitStart, memberKind: ScopeMemberKind.Exception, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length);

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator))
            {
                state.IsActive = false;
            }

            state.SnapshotCreator.StartSampling();
            return DebuggerReturn.GetDefault();
        }

        /// <summary>
        /// End Method with Return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref MethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return new DebuggerReturn<TReturn>(returnValue);
            }

            state.SnapshotCreator.StopSampling();
            state.MethodPhase = EvaluateAt.Exit;

            if (exception != null)
            {
                var captureInfo = new CaptureInfo<Exception>(value: exception, invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.ExitStart, memberKind: ScopeMemberKind.Exception, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length);
                if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator))
                {
                    state.IsActive = false;
                }
            }
            else
            {
                var captureInfo = new CaptureInfo<TReturn>(value: returnValue, name: "@return", invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.ExitStart, memberKind: ScopeMemberKind.Return, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length);
                if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator))
                {
                    state.IsActive = false;
                }

                state.HasLocalsOrReturnValue = true;
            }

            state.SnapshotCreator.StartSampling();
            return new DebuggerReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod_EndMarker(ref MethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            state.SnapshotCreator.StopSampling();
            var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                       state.MethodMetadataInfo.ParameterNames.Length > 0 ||
                                       !state.MethodMetadataInfo.Method.IsStatic;

            var captureInfo = new CaptureInfo<object>(value: state.InvocationTarget, type: state.MethodMetadataInfo.DeclaringType, invocationTargetType: state.MethodMetadataInfo.DeclaringType, memberKind: ScopeMemberKind.This, methodState: MethodState.ExitEnd, hasLocalOrArgument: hasArgumentsOrLocals, method: state.MethodMetadataInfo.Method);
            state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator);
            state.HasLocalsOrReturnValue = false;
        }

        private static MethodDebuggerState CreateInvalidatedDebuggerState()
        {
            var defaultState = MethodDebuggerState.GetDefault();
            defaultState.IsActive = false;
            return defaultState;
        }

        internal static bool TryGetLocalName(int index, string[] localNamesFromPdb, out string localName)
        {
            localName = null;
            if (localNamesFromPdb != null)
            {
                if (index >= localNamesFromPdb.Length)
                {
                    // This is an extra local that does not appear in the PDB. This should only happen if the customer
                    // is using an IL weaving or obfuscation tool that neglects to update the PDB.
                    // There's nothing we can do, so let's just ignore it.
                    return false;
                }

                if (localNamesFromPdb[index] == null)
                {
                    // If the local does not appear in the PDB, then it is a compiler generated local and we shouldn't capture it.
                    return false;
                }
            }

            localName = localNamesFromPdb?[index] ?? "local_" + index;
            return true;
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref MethodDebuggerState state)
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
    }
}
