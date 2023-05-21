// <copyright file="AsyncMethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// AsyncMethodDebuggerInvoker is responsible for the managed side of async method instrumentation,
    /// taking care of creating the state, capturing values and handling exceptions
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AsyncMethodDebuggerInvoker
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncMethodDebuggerInvoker));

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
        /// <param name="probeId">The id of the probe</param>
        /// <param name="probeMetadataIndex">The index used to lookup for the <see cref="ProbeData"/></param>
        /// <param name="instance">Instance value</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="isReEntryToMoveNext">If it the first entry to the state machine MoveNext method</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginMethod<TTarget>(string probeId, int probeMetadataIndex, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex, ref AsyncDebuggerState isReEntryToMoveNext)
        {
            // State machine is null in case is a nested struct inside a generic parent.
            // This can happen if we operate in optimized code and the original async method was inside a generic class
            // or in case the original async method was generic, in which case the state machine is a generic value type
            // See more here: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/method_rewriter.cpp#L70
            if (instance == null)
            {
                isReEntryToMoveNext.LogState = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
                return;
            }

            if (isReEntryToMoveNext.LogState != null)
            {
                // we are in a continuation, return the current state
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
                Log.Warning("BeginMethod: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {Type}, instance type name = {Name}, methodMetadataId = {MethodMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance.GetType().Name, methodMetadataIndex, probeId });
                isReEntryToMoveNext.LogState = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
                return;
            }

            ref var probeData = ref ProbeDataCollection.Instance.TryCreateProbeDataIfNotExists(probeMetadataIndex, probeId);
            if (probeData.IsEmpty())
            {
                Log.Warning("BeginMethod: Failed to receive the ProbeData associated with the executing probe. type = {Type}, instance type name = {Name}, probeMetadataIndex = {ProbeMetadataIndex}, probeId = {ProbeId}", new object[] { typeof(TTarget), instance?.GetType().Name, probeMetadataIndex, probeId });
                isReEntryToMoveNext.LogState = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
                return;
            }

            var asyncState = new AsyncMethodDebuggerState(probeId, ref probeData)
            {
                KickoffInvocationTarget = kickoffInfo.KickoffParentObject,
                StartTime = DateTimeOffset.UtcNow,
                MethodMetadataIndex = methodMetadataIndex,
                MoveNextInvocationTarget = instance
            };

            var activeSpan = Tracer.Instance.InternalActiveScope?.Span;

            if (activeSpan != null)
            {
                ref var methodMetadataInfo = ref asyncState.MethodMetadataInfo;
                activeSpan.Tags.SetTag("source.file_path", methodMetadataInfo.FilePath);
                activeSpan.Tags.SetTag("source.method_begin_line_number", methodMetadataInfo.MethodBeginLineNumber);
                activeSpan.Tags.SetTag("source.method_end_line_number", methodMetadataInfo.MethodEndLineNumber);
            }

            if (!asyncState.SnapshotCreator.ProbeHasCondition &&
                !asyncState.ProbeData.Sampler.Sample())
            {
                isReEntryToMoveNext.LogState = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
                return;
            }

            var hasArgumentsOrLocals = asyncState.HasLocalsOrReturnValue ||
                                       asyncState.HasArguments ||
                                       asyncState.KickoffInvocationTarget != null;

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            var capture = new CaptureInfo<object>(value: asyncState.KickoffInvocationTarget, type: asyncState.MethodMetadataInfo.KickoffInvocationTargetType, methodState: MethodState.EntryAsync, hasLocalOrArgument: hasArgumentsOrLocals, asyncCaptureInfo: asyncCaptureInfo, memberKind: ScopeMemberKind.This, localsCount: asyncState.MethodMetadataInfo.LocalVariableNames.Length, argumentsCount: asyncState.MethodMetadataInfo.ParameterNames.Length);

            asyncState.HasLocalsOrReturnValue = false;
            asyncState.HasArguments = false;

            isReEntryToMoveNext.LogState = asyncState; // Denotes that subsequent re-entries of the `MoveNext` will be ignored by `BeginMethod`.

            if (!asyncState.ProbeData.Processor.Process(ref capture, asyncState.SnapshotCreator))
            {
                isReEntryToMoveNext.LogState.IsActive = false;
            }

            asyncState.SnapshotCreator.StartSampling();
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref AsyncDebuggerState state)
        {
            if (!state.LogState.IsActive)
            {
                return;
            }

            var asyncState = state.LogState;

            asyncState.SnapshotCreator.StopSampling();
            var localVariableNames = asyncState.MethodMetadataInfo.LocalVariableNames;
            if (!MethodDebuggerInvoker.TryGetLocalName(index, localVariableNames, out var localName))
            {
                asyncState.SnapshotCreator.StartSampling();
                return;
            }

            var captureInfo = new CaptureInfo<TLocal>(value: local, methodState: MethodState.LogLocal, name: localName, memberKind: ScopeMemberKind.Local);
            if (!asyncState.ProbeData.Processor.Process(ref captureInfo, asyncState.SnapshotCreator))
            {
                state.LogState.IsActive = false;
            }

            asyncState.HasLocalsOrReturnValue = true;
            asyncState.HasArguments = false;
            asyncState.SnapshotCreator.StartSampling();
        }

        /// <summary>
        /// Logs the return value of <paramref name="methodName"/> in line number ByRef.
        /// </summary>
        /// <typeparam name="TReturnValue">Type of return value.</typeparam>
        /// <param name="returnValue">The local to be logged.</param>
        /// <param name="methodName">index of given argument.</param>
        /// <param name="bytecodeOffset">The bytecode offset of the line where the method call exists.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogCall<TReturnValue>(TReturnValue returnValue, string methodName, int bytecodeOffset, ref AsyncDebuggerState state)
        {
            if (!state.LogState.IsActive)
            {
                return;
            }

            Log.Information("MethodDebuggerInvoker.LogCall with: return value = {ReturnValue}, methodName = {MethodName}", returnValue, methodName);

            // Map between bytecodeOffset -> lineNumber
            ref var methodMetadataInfo = ref state.LogState.MethodMetadataInfo;

            var closestMapping = methodMetadataInfo.ILOffsetToLineNumberMapping
                                                   ?.LastOrDefault(pair => pair.Key <= bytecodeOffset) ?? default;
            var lineNumber = closestMapping.Equals(default(KeyValuePair<int, int>)) ? -1 : closestMapping.Value;
            state.LogState.SnapshotCreator.CaptureReturnValue(returnValue, methodName, lineNumber, returnValue?.GetType() ?? typeof(TReturnValue));
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
        /// <param name="state">Debugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref AsyncDebuggerState state)
        {
            if (!state.LogState.IsActive)
            {
                return DebuggerReturn.GetDefault();
            }

            var asyncState = state.LogState;

            asyncState.SnapshotCreator.StopSampling();
            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            var capture = new CaptureInfo<Exception>(value: exception, methodState: MethodState.ExitStartAsync, asyncCaptureInfo: asyncCaptureInfo, memberKind: ScopeMemberKind.Exception);

            if (!asyncState.ProbeData.Processor.Process(ref capture, asyncState.SnapshotCreator))
            {
                state.LogState.IsActive = false;
            }

            asyncState.SnapshotCreator.StartSampling();
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
        /// <param name="state">Debugger asyncState</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref AsyncDebuggerState state)
        {
            if (!state.LogState.IsActive)
            {
                return new DebuggerReturn<TReturn>(returnValue);
            }

            var asyncState = state.LogState;

            asyncState.SnapshotCreator.StopSampling();
            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            if (exception != null)
            {
                var captureInfo = new CaptureInfo<Exception>(value: exception, methodState: MethodState.ExitStartAsync, memberKind: ScopeMemberKind.Exception, asyncCaptureInfo: asyncCaptureInfo);
                if (!asyncState.ProbeData.Processor.Process(ref captureInfo, asyncState.SnapshotCreator))
                {
                    state.LogState.IsActive = false;
                }
            }
            else if (returnValue != null)
            {
                var captureInfo = new CaptureInfo<TReturn>(value: returnValue, name: "@return", methodState: MethodState.ExitStartAsync, memberKind: ScopeMemberKind.Return, asyncCaptureInfo: asyncCaptureInfo);
                if (!asyncState.ProbeData.Processor.Process(ref captureInfo, asyncState.SnapshotCreator))
                {
                    state.LogState.IsActive = false;
                }

                asyncState.HasLocalsOrReturnValue = true;
            }

            asyncState.SnapshotCreator.StartSampling();
            return new DebuggerReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod_EndMarker(ref AsyncDebuggerState state)
        {
            if (!state.LogState.IsActive)
            {
                return;
            }

            var asyncState = state.LogState;

            asyncState.SnapshotCreator.StopSampling();
            var hasArgumentsOrLocals = asyncState.HasLocalsOrReturnValue ||
                                      asyncState.HasArguments ||
                                      !asyncState.MethodMetadataInfo.Method.IsStatic;

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, asyncState.MethodMetadataInfo.KickoffMethod, asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments, asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals);
            var captureInfo = new CaptureInfo<object>(value: asyncCaptureInfo.KickoffInvocationTarget, type: asyncCaptureInfo.KickoffInvocationTargetType, methodState: MethodState.ExitEndAsync, memberKind: ScopeMemberKind.This, asyncCaptureInfo: asyncCaptureInfo, hasLocalOrArgument: hasArgumentsOrLocals);
            if (!asyncState.ProbeData.Processor.Process(ref captureInfo, asyncState.SnapshotCreator))
            {
                state.LogState.IsActive = false;
            }
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref AsyncDebuggerState state)
        {
            try
            {
                if (state.LogState?.IsActive == false)
                {
                    // Already encountered `LogException`
                    return;
                }

                Log.Warning(exception, "Error caused by our instrumentation");
                state.LogState = AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }
            catch
            {
                // ignored
            }
        }
    }
}
