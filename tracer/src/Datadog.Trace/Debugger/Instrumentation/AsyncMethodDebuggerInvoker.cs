// <copyright file="AsyncMethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
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
        // For case 1, it's simple because we can store all data in AsyncLocal and for each entry to the BeginMethod (through the state machine object),
        // we can inspect the AsyncLocal to see if we already created the context or not
        // but for cases 2 and 2.1 the AsyncLocal of the callee will drive the context from the caller
        // so we can't deterministically know if the context is a continuation or first entry of the callee
        // To address this problem, we adding a new bool field to the state machine object that we setting to true on the first execution,
        // so we can differentiate between the first entry and reentry.

        /// <summary>
        /// We use the AsyncLocal storage to store the debugger state of each logical invocation of the async method i.e. StateMachine.MoveNext run
        /// Therefore, we must ensure that we have the same debugger state on reentry in same context (e.g. continuation that resulted from executing an "await" expression)
        /// rather than a different debugger state when the entry is in a different context (e.g. in a recursive call into the same method).
        /// </summary>
        private static readonly AsyncLocal<AsyncMethodDebuggerState> AsyncContext = new();

        /// <summary>
        /// Create a new context and save the parent.
        /// We call this method from the async kick off method before the state machine is created,
        /// so for each async state machine, this method will be called exactly once.
        /// The IsFirstEntry property of the AsyncMethodDebuggerState will be "true"
        /// then, in the first state machine step, we set this property to "false", thus
        /// we can deduce that any further invocation is not a new logical call but rather a
        /// continuation from an "await" expression.
        /// </summary>
        /// <param name="kickoffInfo">The async kickoff method info</param>
        /// <param name="probeId">The probe ID</param>
        /// <param name="methodMetadataIndex">The method metadata info index</param>
        /// <param name="instance">The instance object of the method</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AsyncMethodDebuggerState SetContext<TTarget>(AsyncHelper.AsyncKickoffMethodInfo kickoffInfo, string probeId, int methodMetadataIndex, TTarget instance)
        {
            var currentState = AsyncContext.Value;
            var newState = new AsyncMethodDebuggerState(probeId)
            {
                Parent = currentState,
                KickoffInvocationTarget = kickoffInfo.KickoffParentObject,
                StartTime = DateTimeOffset.UtcNow,
                MethodMetadataIndex = methodMetadataIndex,
                MoveNextInvocationTarget = instance
            };
            AsyncContext.Value = newState;
            return newState;
        }

        /// <summary>
        /// Restore the async context to the parent
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RestoreContext()
        {
            AsyncContext.Value = AsyncContext.Value?.Parent;
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="instance">Instance value</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="isReEntryToMoveNext">If it the first entry to the state machine MoveNext method</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncMethodDebuggerState BeginMethod<TTarget>(string probeId, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex, ref bool isReEntryToMoveNext)
        {
            if (!ProbeRateLimiter.Instance.Sample(probeId) && !isReEntryToMoveNext)
            {
                return AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }

            // State machine is null in case is a nested struct inside a generic parent.
            // This can happen if we operate in optimized code and the original async method was inside a generic class
            // or in case the original async method was generic, in which case the state machine is a generic value type
            // See more here: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/method_rewriter.cpp#L70
            if (instance == null)
            {
                return AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }

            if (isReEntryToMoveNext)
            {
                // we are in a continuation, return the current state
                return AsyncContext.Value;
            }

            isReEntryToMoveNext = true; // Denotes that subsequent re-entries of the `MoveNext` will be ignored by `BeginMethod`.

            var stateMachineType = instance.GetType();
            var kickoffInfo = AsyncHelper.GetAsyncKickoffMethodInfo(instance, stateMachineType);
            if (kickoffInfo.KickoffParentObject == null && kickoffInfo.KickoffMethod.IsStatic == false)
            {
                Log.Warning($"{nameof(BeginMethod)}: hoisted 'this' has not found. {kickoffInfo.KickoffParentType.Name}.{kickoffInfo.KickoffMethod.Name}");
            }

            if (!MethodMetadataProvider.TryCreateAsyncMethodMetadataIfNotExists(instance, methodMetadataIndex, in methodHandle, in typeHandle, kickoffInfo))
            {
                Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance.GetType().Name}, methodMetadataId = {methodMetadataIndex}");
                return AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }

            // we are not in a continuation, create new state and capture everything
            var asyncState = SetContext(kickoffInfo, probeId, methodMetadataIndex, instance);

            var hasArgumentsOrLocals = asyncState.HasLocalsOrReturnValue ||
                                       asyncState.HasArguments ||
                                       asyncState.KickoffInvocationTarget != null;

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            var capture = new CaptureInfo<object>(value: asyncState.KickoffInvocationTarget, methodState: MethodState.EntryAsync, type: asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hasLocalOrArgument: hasArgumentsOrLocals, asyncCaptureInfo: asyncCaptureInfo);
            if (!ProbeExpressionsProcessor.Instance.Process(probeId, ref capture, asyncState.SnapshotCreator))
            {
                asyncState.IsActive = false;
            }

            asyncState.HasLocalsOrReturnValue = false;
            asyncState.HasArguments = false;
            AsyncContext.Value = asyncState;
            return AsyncContext.Value;
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="asyncState">Debugger asyncState</param>
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

            var captureInfo = new CaptureInfo<TLocal>(value: local, type: typeof(TLocal), methodState: MethodState.LogLocal, name: localName, memberKind: ScopeMemberKind.Local);
            if (!ProbeExpressionsProcessor.Instance.Process(asyncState.ProbeId, ref captureInfo, asyncState.SnapshotCreator))
            {
                asyncState.IsActive = false;
            }

            asyncState.HasLocalsOrReturnValue = true;
            asyncState.HasArguments = false;
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
        /// <param name="asyncState">Debugger asyncState</param>
        /// <returns>LiveDebugger return structure</returns>
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref AsyncMethodDebuggerState asyncState)
        {
            if (!asyncState.IsActive)
            {
                return DebuggerReturn.GetDefault();
            }

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            var capture = new CaptureInfo<Exception>(value: exception, methodState: MethodState.ExitStartAsync, type: exception?.GetType(), asyncCaptureInfo: asyncCaptureInfo, memberKind: ScopeMemberKind.Exception);
            if (!ProbeExpressionsProcessor.Instance.Process(asyncState.ProbeId, ref capture, asyncState.SnapshotCreator))
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

            var asyncCaptureInfo = new AsyncCaptureInfo(asyncState.MoveNextInvocationTarget, asyncState.KickoffInvocationTarget, asyncState.MethodMetadataInfo.KickoffInvocationTargetType, hoistedLocals: asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals, hoistedArgs: asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments);
            if (exception != null)
            {
                var captureInfo = new CaptureInfo<Exception>(value: exception, type: exception.GetType(), methodState: MethodState.ExitStartAsync, memberKind: ScopeMemberKind.Exception, asyncCaptureInfo: asyncCaptureInfo);
                if (!ProbeExpressionsProcessor.Instance.Process(asyncState.ProbeId, ref captureInfo, asyncState.SnapshotCreator))
                {
                    asyncState.IsActive = false;
                }
            }

            if (returnValue != null)
            {
                var captureInfo = new CaptureInfo<TReturn>(value: returnValue, name: "@return", type: typeof(TReturn), methodState: MethodState.ExitStartAsync, memberKind: ScopeMemberKind.Return, asyncCaptureInfo: asyncCaptureInfo);
                if (!ProbeExpressionsProcessor.Instance.Process(asyncState.ProbeId, ref captureInfo, asyncState.SnapshotCreator))
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
        /// <param name="asyncState">Debugger asyncState</param>
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
            var captureInfo = new CaptureInfo<object>(value: asyncCaptureInfo.KickoffInvocationTarget, type: asyncCaptureInfo.KickoffInvocationTargetType, methodState: MethodState.ExitEndAsync, memberKind: ScopeMemberKind.This, asyncCaptureInfo: asyncCaptureInfo, hasLocalOrArgument: hasArgumentsOrLocals);
            if (!ProbeExpressionsProcessor.Instance.Process(asyncState.ProbeId, ref captureInfo, asyncState.SnapshotCreator))
            {
                asyncState.IsActive = false;
            }

            RestoreContext();
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="asyncState">Debugger asyncState</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref AsyncMethodDebuggerState asyncState)
        {
            if (!asyncState.IsActive)
            {
                // Already encountered `LogException`
                return;
            }

            Log.Warning(exception, "Error caused by our instrumentation");
            asyncState.IsActive = false;
            RestoreContext();
        }
    }
}
