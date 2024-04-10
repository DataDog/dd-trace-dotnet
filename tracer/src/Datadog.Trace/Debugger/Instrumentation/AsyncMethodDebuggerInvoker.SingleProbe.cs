// <copyright file="AsyncMethodDebuggerInvoker.SingleProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// AsyncMethodDebuggerInvoker is responsible for the managed side of async method instrumentation,
    /// taking care of creating the state, capturing values and handling exceptions
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial class AsyncMethodDebuggerInvoker
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncMethodDebuggerInvoker));

        /// <summary>
        /// Determines if the instrumentation should call <see cref="UpdateProbeInfo{T}"/>.
        /// </summary>
        /// <param name="methodMetadataIndex">The unique index of the method.</param>
        /// <param name="instrumentationVersion">The unique identifier of the instrumentation.</param>
        /// <param name="state">If it the first entry to the state machine MoveNext method</param>
        /// <returns>true if <see cref="UpdateProbeInfo{T}"/> should be called, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldUpdateProbeInfo(int methodMetadataIndex, int instrumentationVersion, ref AsyncDebuggerState state)
        {
            if (!MethodMetadataCollection.Instance.IndexExists(methodMetadataIndex))
            {
                return true;
            }

            ref var methodMetadataInfo = ref MethodMetadataCollection.Instance.Get(methodMetadataIndex);

            var isDifferentVersion = methodMetadataInfo.InstrumentationVersion != instrumentationVersion;

            if (IsReEnter(ref state) && isDifferentVersion)
            {
                // Re-enter but with different instrumentation version. Ditching state and avoiding the collection of further instrumentation data.
                state.LogStates = AsyncMethodDebuggerState.CreateInvalidatedDebuggerStates();
                return false;
            }

            return isDifferentVersion;
        }

        /// <summary>
        /// Updates the ProbeIds and ProbeMetadataIndices associated with the <see cref="MethodMetadataInfo"/> associated with the given <paramref name="methodMetadataIndex"/> and sets the corresponding <paramref name="instrumentationVersion"/>.
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="probeIds">Probe Ids</param>
        /// <param name="probeMetadataIndices">Probe Metadata Indices</param>
        /// <param name="instance">Instance value</param>
        /// <param name="methodMetadataIndex">The unique index of the method.</param>
        /// <param name="instrumentationVersion">The version of this particular instrumentation.</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateProbeInfo<TTarget>(
            string[] probeIds,
            int[] probeMetadataIndices,
            TTarget instance,
            int methodMetadataIndex,
            int instrumentationVersion,
            RuntimeMethodHandle methodHandle,
            RuntimeTypeHandle typeHandle)
        {
            // State machine is null in case is a nested struct inside a generic parent.
            // This can happen if we operate in optimized code and the original async method was inside a generic class
            // or in case the original async method was generic, in which case the state machine is a generic value type
            // See more here: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/method_rewriter.cpp#L70
            if (instance == null)
            {
                return;
            }

            var stateMachineType = instance.GetType();
            var kickoffInfo = AsyncHelper.GetAsyncKickoffMethodInfo(instance, stateMachineType);
            if (kickoffInfo.KickoffParentObject == null && kickoffInfo.KickoffMethod.IsStatic == false)
            {
                Log.Warning(nameof(BeginMethod) + ": hoisted 'this' has not found. {KickoffParentType}.{KickoffMethod}", kickoffInfo.KickoffParentType.Name, kickoffInfo.KickoffMethod.Name);
            }

            if (!MethodMetadataCollection.Instance.TryCreateAsyncMethodMetadataIfNotExists(instance, methodMetadataIndex, in methodHandle, in typeHandle, kickoffInfo))
            {
                Log.Warning("BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. methodMetadataId = {MethodMetadataIndex}, instrumentationVersion = {InstrumentationVersion}", new object[] { methodMetadataIndex, instrumentationVersion });
                return;
            }

            MethodMetadataCollection.Instance.Get(methodMetadataIndex).Update(probeIds, probeMetadataIndices, instrumentationVersion);
        }

        /// <summary>
        /// Determines if the instrumentation should call <see cref="UpdateProbeInfo{T}"/>.
        /// </summary>
        /// <param name="state">If it the first entry to the state machine MoveNext method</param>
        /// <returns>true if we are re-entering into the MoveNext, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsReEnter(ref AsyncDebuggerState state)
        {
            return state.LogStates != null;
        }

        // We have two scenarios:
        // 1. async method that does not participate in any other async method in i.e. has no async caller: probe placed on only one async method.
        // 2. async method that has been called from another async method (async caller) -
        // i.e. a probe was placed on two async methods that have a caller callee relationship (e.g. Async1 calls Async2 and we have probes on both of them)
        // 2.1. async method that participate in a recursive call so we enter the same state machine type more than once but in a different context
        // For case 1, it's simple because we can store all data in the field we embed during module load and for each entry to the BeginMethod (through the state machine object),
        // To address them, we embed a field inside the State Machine of type `AsyncMethodDebuggerState` and use it to determine if we are
        // in a reenter by simply checking if it's null, in case it's not we can just use it to keep collecting contextual information.

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="probeMetadataIndex">The index used to lookup for the <see cref="ProbeData"/></param>
        /// <param name="instrumentationVersion">The version of this particular instrumentation.</param>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="state">If it the first entry to the state machine MoveNext method</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginMethod<TTarget>(TTarget instance, int methodMetadataIndex, int probeMetadataIndex, int instrumentationVersion, string probeId, ref AsyncMethodDebuggerState state)
        {
            if (!MethodMetadataCollection.Instance.IndexExists(methodMetadataIndex))
            {
                Log.Warning("BeginMethod: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {Type}, instance type name = {Name}, methodMetadaId = {MethodMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, methodMetadataIndex, probeId });
                state = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
                return;
            }

            // State machine is null in case is a nested struct inside a generic parent.
            // This can happen if we operate in optimized code and the original async method was inside a generic class
            // or in case the original async method was generic, in which case the state machine is a generic value type
            // See more here: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/method_rewriter.cpp#L70
            if (instance == null)
            {
                state = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
                return;
            }

            var stateMachineType = instance.GetType();
            var kickoffInfo = AsyncHelper.GetAsyncKickoffMethodInfo(instance, stateMachineType);
            if (kickoffInfo.KickoffParentObject == null && kickoffInfo.KickoffMethod.IsStatic == false)
            {
                Log.Warning(nameof(BeginMethod) + ": hoisted 'this' has not found. {KickoffParentType}.{KickoffMethod}", kickoffInfo.KickoffParentType.Name, kickoffInfo.KickoffMethod.Name);
            }

            ref var probeData = ref ProbeDataCollection.Instance.TryCreateProbeDataIfNotExists(probeMetadataIndex, probeId);
            if (probeData.IsEmpty())
            {
                Log.Warning("BeginMethod: Failed to receive the ProbeData associated with the executing probe. type = {Type}, instance type name = {Name}, probeMetadataIndex = {ProbeMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, probeMetadataIndex, probeId });
                state = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
                return;
            }

            if (!probeData.Processor.ShouldProcess(in probeData))
            {
                Log.Warning("BeginMethod: Skipping the instrumentation. type = {Type}, instance type name = {Name}, probeMetadataIndex = {ProbeMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, probeMetadataIndex, probeId });
                state = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
                return;
            }

            var asyncState = new AsyncMethodDebuggerState(probeId, ref probeData)
            {
                KickoffInvocationTarget = kickoffInfo.KickoffParentObject,
                StartTime = DateTimeOffset.UtcNow,
                MethodMetadataIndex = methodMetadataIndex,
                MoveNextInvocationTarget = instance
            };

            var hasArgumentsOrLocals = asyncState.HasLocalsOrReturnValue ||
                                       asyncState.HasArguments ||
                                       asyncState.KickoffInvocationTarget != null;

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            var capture = new CaptureInfo<object>(asyncState.MethodMetadataIndex, method: asyncState.MethodMetadataInfo.Method, value: asyncState.KickoffInvocationTarget, type: asyncState.MethodMetadataInfo.KickoffInvocationTargetType, methodState: MethodState.EntryAsync, hasLocalOrArgument: hasArgumentsOrLocals, asyncCaptureInfo: asyncCaptureInfo, memberKind: ScopeMemberKind.This, localsCount: asyncState.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: asyncState.MethodMetadataInfo.ParameterNames.Length);

            asyncState.HasLocalsOrReturnValue = false;
            asyncState.HasArguments = false;

            state = asyncState; // Denotes that subsequent re-entries of the `MoveNext` will be ignored by `BeginMethod`.

            if (!asyncState.ProbeData.Processor.Process(ref capture, asyncState.SnapshotCreator, in probeData))
            {
                asyncState.IsActive = false;
            }
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="asyncState">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref AsyncMethodDebuggerState asyncState)
        {
            if (!asyncState.IsActive)
            {
                return;
            }

            var localVariableNames = asyncState.MethodMetadataInfo.LocalVariableNames;
            if (!MethodDebuggerInvoker.TryGetLocalName(index, localVariableNames, out var localName))
            {
                return;
            }

            if (!ProcessMoveNextLocal(ref local, asyncState, localName))
            {
                asyncState.IsActive = false;
            }

            asyncState.HasLocalsOrReturnValue = true;
            asyncState.HasArguments = false;
        }

        private static bool ProcessMoveNextLocal<TLocal>(ref TLocal local, AsyncMethodDebuggerState asyncState, string localName)
        {
            var probeData = asyncState.ProbeData;
            if (!TypeExtensions.IsDefaultValue(ref local))
            {
                var localInfo = new CaptureInfo<TLocal>(asyncState.MethodMetadataIndex, value: local, methodState: MethodState.LogLocal, name: localName, memberKind: ScopeMemberKind.Local);
                return asyncState.ProbeData.Processor.Process(ref localInfo, asyncState.SnapshotCreator, in probeData);
            }

            var unreachableLocal = new DebuggerSnapshotSerializer.UnreachableLocal(DebuggerSnapshotSerializer.UnreachableLocalReason.NotHoistedLocalInAsyncMethod);
            var captureInfo = new CaptureInfo<DebuggerSnapshotSerializer.UnreachableLocal>(asyncState.MethodMetadataIndex, value: unreachableLocal, type: typeof(TLocal), methodState: MethodState.LogLocal, name: localName, memberKind: ScopeMemberKind.Local);
            return asyncState.ProbeData.Processor.Process(ref captureInfo, asyncState.SnapshotCreator, in probeData);
        }

        /// <summary>
        /// End Method with void return value
        /// This method is called from either (1) the outer-most catch clause when the async method threw exception
        /// or (2) when the async method has logically ended.
        /// In this phase we have the correct async context in hand because we already set it in the BeginMethod.
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="asyncState">Debugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref AsyncMethodDebuggerState asyncState)
        {
            if (!asyncState.IsActive)
            {
                return DebuggerReturn.GetDefault();
            }

            asyncState.MoveNextInvocationTarget = instance;

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            var capture = new CaptureInfo<Exception>(asyncState.MethodMetadataIndex, value: exception, methodState: MethodState.ExitStartAsync, localsCount: asyncState.MethodMetadataInfo.LocalVariableNames.Length, asyncCaptureInfo: asyncCaptureInfo, memberKind: ScopeMemberKind.Exception);
            var probeData = asyncState.ProbeData;

            if (!asyncState.ProbeData.Processor.Process(ref capture, asyncState.SnapshotCreator, in probeData))
            {
                asyncState.IsActive = false;
            }

            return DebuggerReturn.GetDefault();
        }

        /// <summary>
        /// End Method with Return value - the MoveNext method always returns void but here we send the kick-off method's return value
        /// This method is called from either (1) the outer-most catch clause when the async method threw exception
        /// or (2) when the async method has logically ended.
        /// In this phase we have the correct async context in hand because we already set it in the BeginMethod.
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="asyncState">Debugger asyncState</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref AsyncMethodDebuggerState asyncState)
        {
            if (!asyncState.IsActive)
            {
                return new DebuggerReturn<TReturn>(returnValue);
            }

            asyncState.MoveNextInvocationTarget = instance;

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            var probeData = asyncState.ProbeData;

            if (exception != null)
            {
                var captureInfo = new CaptureInfo<Exception>(asyncState.MethodMetadataIndex, value: exception, methodState: MethodState.ExitStartAsync, memberKind: ScopeMemberKind.Exception, localsCount: asyncState.MethodMetadataInfo.LocalVariableNames.Length, asyncCaptureInfo: asyncCaptureInfo);
                if (!asyncState.ProbeData.Processor.Process(ref captureInfo, asyncState.SnapshotCreator, in probeData))
                {
                    asyncState.IsActive = false;
                }
            }
            else if (returnValue != null)
            {
                var captureInfo = new CaptureInfo<TReturn>(asyncState.MethodMetadataIndex, value: returnValue, name: "@return", methodState: MethodState.ExitStartAsync, memberKind: ScopeMemberKind.Return, localsCount: asyncState.MethodMetadataInfo.LocalVariableNames.Length, asyncCaptureInfo: asyncCaptureInfo);
                if (!asyncState.ProbeData.Processor.Process(ref captureInfo, asyncState.SnapshotCreator, in probeData))
                {
                    asyncState.IsActive = false;
                }

                asyncState.HasLocalsOrReturnValue = true;
            }

            return new DebuggerReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <param name="asyncState">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod_EndMarker(ref AsyncMethodDebuggerState asyncState)
        {
            if (!asyncState.IsActive)
            {
                return;
            }

            var hasArgumentsOrLocals = asyncState.HasLocalsOrReturnValue ||
                                      asyncState.HasArguments ||
                                      !asyncState.MethodMetadataInfo.Method.IsStatic;

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, asyncState.MethodMetadataInfo.KickoffMethod, asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments, asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals);
            var captureInfo = new CaptureInfo<object>(asyncState.MethodMetadataIndex, value: asyncCaptureInfo.KickoffInvocationTarget, type: asyncCaptureInfo.KickoffInvocationTargetType, methodState: MethodState.ExitEndAsync, memberKind: ScopeMemberKind.This, asyncCaptureInfo: asyncCaptureInfo, hasLocalOrArgument: hasArgumentsOrLocals);
            var probeData = asyncState.ProbeData;

            if (!asyncState.ProbeData.Processor.Process(ref captureInfo, asyncState.SnapshotCreator, in probeData))
            {
                asyncState.IsActive = false;
            }
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="asyncState">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref AsyncMethodDebuggerState asyncState)
        {
            try
            {
                if (asyncState?.IsActive == false)
                {
                    // Already encountered `LogException`
                    return;
                }

                Log.Warning(exception, "Error caused by our instrumentation");
                asyncState.ProbeData.Processor.LogException(exception, asyncState.SnapshotCreator);
            }
            catch
            {
                // Ignored
            }
            finally
            {
                asyncState = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }
        }
    }
}
