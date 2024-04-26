// <copyright file="MethodDebuggerInvoker.SingleProbe.cs" company="Datadog">
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
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// MethodDebuggerInvoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial class MethodDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodDebuggerInvoker));

        /// <summary>
        /// Determines if the instrumentation should call <see cref="UpdateProbeInfo"/>.
        /// </summary>
        /// <param name="methodMetadataIndex">The unique index of the method.</param>
        /// <param name="instrumentationVersion">The unique identifier of the instrumentation.</param>
        /// <returns>true if <see cref="UpdateProbeInfo"/> should be called, false otherwise.</returns>
        public static bool ShouldUpdateProbeInfo(int methodMetadataIndex, int instrumentationVersion)
        {
            if (!MethodMetadataCollection.Instance.IndexExists(methodMetadataIndex))
            {
                return true;
            }

            return MethodMetadataCollection.Instance.Get(methodMetadataIndex).InstrumentationVersion != instrumentationVersion;
        }

        /// <summary>
        /// Updates the ProbeIds and ProbeMetadataIndices associated with the <see cref="MethodMetadataInfo"/> associated with the given <paramref name="methodMetadataIndex"/> and sets the corresponding <paramref name="instrumentationVersion"/>.
        /// </summary>
        /// <param name="probeIds">Probe Ids</param>
        /// <param name="probeMetadataIndices">Probe Metadata Indices</param>
        /// <param name="methodMetadataIndex">The unique index of the method.</param>
        /// <param name="instrumentationVersion">The version of this particular instrumentation.</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        public static void UpdateProbeInfo(
            string[] probeIds,
            int[] probeMetadataIndices,
            int methodMetadataIndex,
            int instrumentationVersion,
            RuntimeMethodHandle methodHandle,
            RuntimeTypeHandle typeHandle)
        {
            if (!MethodMetadataCollection.Instance.TryCreateNonAsyncMethodMetadataIfNotExists(methodMetadataIndex, in methodHandle, in typeHandle))
            {
                Log.Warning("BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. methodMetadataId = {MethodMetadataIndex}, instrumentationVersion = {InstrumentationVersion}", new object[] { methodMetadataIndex, instrumentationVersion });
                return;
            }

            MethodMetadataCollection.Instance.Get(methodMetadataIndex).Update(probeIds, probeMetadataIndices, instrumentationVersion);
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="probeMetadataIndex">The index used to lookup for the <see cref="ProbeData"/></param>
        /// <param name="probeId">The id of the probe</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodDebuggerState BeginMethod_StartMarker<TTarget>(TTarget instance, int methodMetadataIndex, int probeMetadataIndex, string probeId)
        {
            if (!MethodMetadataCollection.Instance.IndexExists(methodMetadataIndex))
            {
                Log.Warning("BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {Type}, instance type name = {Name}, methodMetadaId = {MethodMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, methodMetadataIndex, probeId });
                return CreateInvalidatedDebuggerState();
            }

            ref var probeData = ref ProbeDataCollection.Instance.TryCreateProbeDataIfNotExists(probeMetadataIndex, probeId);
            if (probeData.IsEmpty())
            {
                Log.Warning("BeginMethod_StartMarker: Failed to receive the ProbeData associated with the executing probe. type = {Type}, instance type name = {Name}, probeMetadataIndex = {ProbeMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, probeMetadataIndex, probeId });
                return CreateInvalidatedDebuggerState();
            }

            if (!probeData.Processor.ShouldProcess(in probeData))
            {
                Log.Warning("BeginMethod_StartMarker: Skipping the instrumentation. type = {Type}, instance type name = {Name}, probeMetadataIndex = {ProbeMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, probeMetadataIndex, probeId });
                return CreateInvalidatedDebuggerState();
            }

            var state = new MethodDebuggerState(probeId, scope: default, methodMetadataIndex, ref probeData, instance);

            var captureInfo = new CaptureInfo<Type>(state.MethodMetadataIndex, value: null, method: state.MethodMetadataInfo.Method, type: state.MethodMetadataInfo.DeclaringType, invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.EntryStart, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length);

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData))
            {
                state.IsActive = false;
            }

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

            var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                       state.MethodMetadataInfo.ParameterNames.Length > 0 ||
                                       !state.MethodMetadataInfo.Method.IsStatic;

            var captureInfo = new CaptureInfo<object>(state.MethodMetadataIndex, value: state.InvocationTarget, type: state.MethodMetadataInfo.DeclaringType, invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.EntryEnd, hasLocalOrArgument: hasArgumentsOrLocals, memberKind: ScopeMemberKind.This);
            var probeData = state.ProbeData;

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData))
            {
                state.IsActive = false;
            }

            state.HasLocalsOrReturnValue = false;
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

            var paramName = state.MethodMetadataInfo.ParameterNames[index];
            var captureInfo = new CaptureInfo<TArg>(state.MethodMetadataIndex, value: arg, methodState: MethodState.LogArg, name: paramName, memberKind: ScopeMemberKind.Argument);
            var probeData = state.ProbeData;

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData))
            {
                state.IsActive = false;
            }

            state.HasLocalsOrReturnValue = false;
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

            var localVariableNames = state.MethodMetadataInfo.LocalVariableNames;
            if (!TryGetLocalName(index, localVariableNames, out var localName))
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

            state.MethodPhase = EvaluateAt.Exit;
            state.InvocationTarget = instance;

            var captureInfo = new CaptureInfo<Exception>(state.MethodMetadataIndex, value: exception, invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.ExitStart, memberKind: ScopeMemberKind.Exception, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length);
            var probeData = state.ProbeData;

            if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData))
            {
                state.IsActive = false;
            }

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

            state.MethodPhase = EvaluateAt.Exit;
            state.InvocationTarget = instance;
            var probeData = state.ProbeData;

            if (exception != null)
            {
                var captureInfo = new CaptureInfo<Exception>(state.MethodMetadataIndex, value: exception, invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.ExitStart, memberKind: ScopeMemberKind.Exception, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length);
                if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData))
                {
                    state.IsActive = false;
                }
            }
            else
            {
                var captureInfo = new CaptureInfo<TReturn>(state.MethodMetadataIndex, value: returnValue, name: "@return", invocationTargetType: state.MethodMetadataInfo.DeclaringType, methodState: MethodState.ExitStart, memberKind: ScopeMemberKind.Return, localsCount: state.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: state.MethodMetadataInfo.ParameterNames.Length);
                if (!state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData))
                {
                    state.IsActive = false;
                }

                state.HasLocalsOrReturnValue = true;
            }

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

            var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                       state.MethodMetadataInfo.ParameterNames.Length > 0 ||
                                       !state.MethodMetadataInfo.Method.IsStatic;

            var captureInfo = new CaptureInfo<object>(state.MethodMetadataIndex, value: state.InvocationTarget, type: state.MethodMetadataInfo.DeclaringType, invocationTargetType: state.MethodMetadataInfo.DeclaringType, memberKind: ScopeMemberKind.This, methodState: MethodState.ExitEnd, hasLocalOrArgument: hasArgumentsOrLocals, method: state.MethodMetadataInfo.Method);
            var probeData = state.ProbeData;

            state.ProbeData.Processor.Process(ref captureInfo, state.SnapshotCreator, in probeData);
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
                state.ProbeData.Processor.LogException(exception, state.SnapshotCreator);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Used to clean up resources. Called upon entering the (instrumentation's) finally block.
        /// </summary>
        /// <param name="methodMetadataIndex">The unique index of the method.</param>
        /// <param name="instrumentationVersion">The unique identifier of the instrumentation.</param>
        /// <param name="state">Debugger states</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose(int methodMetadataIndex, int instrumentationVersion, ref MethodDebuggerState state)
        {
            // Should clean up the state. E.g: If we retrieved it from an Object Pool, we should return it here.
            InstrumentationAllocator.ReturnObject(ref state);
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
