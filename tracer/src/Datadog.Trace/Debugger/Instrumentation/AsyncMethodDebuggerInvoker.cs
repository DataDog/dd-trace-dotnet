// <copyright file="AsyncMethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// AsyncMethodDebuggerInvoker is responsible for the managed side of async method instrumentation,
    /// taking care of creating the state, capturing values and handling exceptions
    /// </summary>
    public static class AsyncMethodDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncMethodDebuggerInvoker));

        // We have two scenarios:
        // 1. async method that does not participate in any other async method in i.e. has not async caller: probe on only one async method
        // 2. async method that has been called from another async method (async caller) - i.e. probe on two async methods, that has a caller callee relationship: Async1 call to Async2 and we have probes on both of them
        // 2.1. async method that participate in a recursive call so we enter the same state machine type more than once but in a different context
        // For case 1 it's simple because we can store all data in AsyncLocal and for each entry to the BeginMethod (through the state machine object)
        // we can inspect the AsyncLocal to see if we already create the context ot not
        // but for case 2 and 2.1 the AsyncLocal of the callee will drive the context from the caller
        // so we can't deterministic know if the context is a continuation or first entry of the callee

        // We use the AsyncLocal storage to store the debugger state of each logical invocation of the async method i.e. StateMachine.MoveNext run
        // We want to have the same debugger state when reentry in same context (e.g. continuation after executing an "await" expression) but a different debugger state when the entry is in a different context

        /// <summary>
        /// We use the AsyncLocal storage to store the debugger state of each logical invocation of the async method i.e. StateMachine.MoveNext run
        /// We want to have the same debugger state when reentry in same context (e.g. continuation after executing an "await" expression) but a different debugger state when the entry is in a different context
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
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">The instance (target object) of the method</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncMethodDebuggerState SetContext<TTarget>(TTarget instance)
        {
            var currentState = AsyncContext.Value;
            var newState = new AsyncMethodDebuggerState { Parent = currentState, KickoffInvocationTarget = instance, InvocationTargetType = typeof(TTarget) };
            AsyncContext.Value = newState;
            return newState;
        }

        /// <summary>
        /// Restore the async context to the parent
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RestoreContext()
        {
            AsyncContext.Value = AsyncContext.Value.Parent;
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
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncMethodDebuggerState BeginMethod<TTarget>(string probeId, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex)
        {
            if (ProbeRateLimiter.Instance.IsLimitReached)
            {
                return AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }

            var asyncState = AsyncContext.Value;
            if (asyncState is { IsFirstEntry: false })
            {
                // We are not logically entering the async method - rather, we are in a continuation (resuming after an 'await' expression),
                // return the current async state without capture anything
                return asyncState;
            }

            if (!MethodMetadataProvider.TryCreateIfNotExists(instance, methodMetadataIndex, in methodHandle, in typeHandle))
            {
                Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadataId = {methodMetadataIndex}");
                return AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }

            // We are not in a continuation,
            // i.e. there is no context or the context just created in the kick off method but the state machine not yet run
            // so create a new one and capture everything
            asyncState.ProbeId = probeId;
            asyncState.StartTime = DateTimeOffset.UtcNow;
            asyncState.MethodMetadataIndex = methodMetadataIndex;
            asyncState.IsFirstEntry = false;
            asyncState.MoveNextInvocationTarget = instance;

            BeginMethodStartMarker(ref asyncState);

            BeginMethodLogArgs(instance, ref asyncState);

            asyncState = BeginMethodEndMarker(ref asyncState);

            AsyncContext.Value = asyncState;
            return AsyncContext.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BeginMethodStartMarker(ref AsyncMethodDebuggerState asyncState)
        {
            asyncState.SnapshotCreator.StartDebugger();
            asyncState.SnapshotCreator.StartSnapshot();
            asyncState.SnapshotCreator.StartCaptures();
            asyncState.SnapshotCreator.StartEntry();
            asyncState.SnapshotCreator.CaptureInstance(asyncState.KickoffInvocationTarget, asyncState.InvocationTargetType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BeginMethodLogArgs<TTarget>(TTarget instance, ref AsyncMethodDebuggerState asyncState)
        {
            // capture hoisted arguments
            var kickOffMethodParameters = AsyncHelper.GetAsyncKickoffMethod(asyncState.MethodMetadataInfo.DeclaringType).GetParameters();
            var kickOffMethodArgumentsValues = AsyncHelper.GetHoistedArgumentsFromStateMachine(instance, kickOffMethodParameters);
            for (var index = 0; index < kickOffMethodArgumentsValues.Length; index++)
            {
                ref var parameter = ref kickOffMethodArgumentsValues[index];
                if (parameter == default)
                {
                    continue;
                }

                var parameterValue = parameter.Value;
                LogArg(ref parameterValue, parameter.Name, index, ref asyncState);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AsyncMethodDebuggerState BeginMethodEndMarker(ref AsyncMethodDebuggerState asyncState)
        {
            var hasArgumentsOrLocals = asyncState.HasLocalsOrReturnValue ||
                                       asyncState.HasArguments;

            asyncState.HasLocalsOrReturnValue = false;
            asyncState.HasArguments = false;
            asyncState.SnapshotCreator.EndEntry(hasArgumentsOrLocals);
            return asyncState;
        }

        /// <summary>
        /// Logs the given <paramref name="arg"/> ByRef.
        /// </summary>
        /// <typeparam name="TArg">Type of argument.</typeparam>
        /// <param name="arg">The argument to be logged.</param>
        /// <param name="paramName">The argument name</param>
        /// <param name="index">Index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(ref TArg arg, string paramName, int index, ref AsyncMethodDebuggerState state)
        {
            state.SnapshotCreator.CaptureArgument(arg, paramName, index == 0, state.HasLocalsOrReturnValue);
            state.HasLocalsOrReturnValue = false;
            state.HasArguments = true;
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        /// https://datadoghq.atlassian.net/browse/DEBUG-928
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref AsyncMethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            var localNamesFromPdb = state.MethodMetadataInfo.LocalVariableNames;
            if (!MethodDebuggerInvoker.TryGetLocalName(index, localNamesFromPdb, out var localName))
            {
                return;
            }

            state.SnapshotCreator.CaptureLocal(local, localName, index == 0 && !state.HasLocalsOrReturnValue);
            state.HasLocalsOrReturnValue = true;
            state.HasArguments = true;
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// This method is called from either (1) the outer-most catch clause when the async method threw exception
        /// or (2) when the async method has logically ended.
        /// In this phase we have the correct async context in hand because we already set it in the BeginMethod.
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        /// <returns>CallTarget return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref AsyncMethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return DebuggerReturn.GetDefault();
            }

            state.SnapshotCreator.StartReturn();
            state.SnapshotCreator.CaptureInstance(instance, instance.GetType());
            if (exception != null)
            {
                state.SnapshotCreator.CaptureException(exception);
            }

            EndMethodLogLocals(ref state);
            return DebuggerReturn.GetDefault();
        }

        /// <summary>
        /// End Method with Return value invoker - the MoveNext method always return void but here we send the kick-of method return value
        /// This method is called from either (1) the outer-most catch clause when the async method threw exception
        /// or (2) when the async method has logically ended.
        /// In this phase we have the correct async context in hand because we already set it in the BeginMethod.
        /// </summary>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TReturn, TTarget>(TTarget instance, TReturn returnValue, Exception exception, ref AsyncMethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return new DebuggerReturn<TReturn>(returnValue);
            }

            state.SnapshotCreator.StartReturn();
            state.SnapshotCreator.CaptureInstance(instance, instance.GetType());
            if (exception != null)
            {
                state.SnapshotCreator.CaptureException(exception);
            }
            else
            {
                state.SnapshotCreator.CaptureLocal(returnValue, "@return", true);
                state.HasLocalsOrReturnValue = true;
            }

            EndMethodLogLocals(ref state);
            return new DebuggerReturn<TReturn>(returnValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EndMethodLogLocals(ref AsyncMethodDebuggerState asyncState)
        {
            // MethodMetadataInfo saves locals from MoveNext localVarSig,
            // this isn't enough in async scenario because we need to extract more locals the may hoisted in the builder object
            // and we need to subtract some locals that exist in the localVarSig but they are not belongs to the kickoff method
            // For know we capturing here all locals the are hoisted (except known generated locals)
            // and we capturing in LogLocal the locals form localVarSig
            var kickOffMethodLocalsValues = asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals;
            for (var index = 0; index < kickOffMethodLocalsValues.Length; index++)
            {
                var local = kickOffMethodLocalsValues[index];
                asyncState.SnapshotCreator.CaptureLocal(local.GetValue(asyncState.MoveNextInvocationTarget), local.Name, index == 0 && !asyncState.HasLocalsOrReturnValue);
                asyncState.HasLocalsOrReturnValue = true;
                asyncState.HasArguments = false;
            }
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod_EndMarker(ref AsyncMethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                       state.HasArguments;
            state.SnapshotCreator.MethodProbeEndReturn(hasArgumentsOrLocals);
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref AsyncMethodDebuggerState state)
        {
            Log.Error(exception, "Error caused by our instrumentation");
            state.IsActive = false;
        }

        /// <summary>
        /// Finalize snapshot
        /// </summary>
        /// <param name="state">The async debugger state</param>
        /// <param name="task">Task builder task</param>
        /// <param name="stackFrames">Stack frames of the kick-off method</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FinalizeSnapshot(ref AsyncMethodDebuggerState state, Task task, StackFrame[] stackFrames)
        {
            if (task.IsCanceled)
            {
                return;
            }

            using (state.SnapshotCreator)
            {
                var methodName = state.MethodMetadataInfo.Method?.Name;
                var typeFullName = state.MethodMetadataInfo.DeclaringType?.FullName;

                state.SnapshotCreator.AddMethodProbeInfo(
                          state.ProbeId,
                          methodName,
                          typeFullName)
                     .FinalizeSnapshot(
                              stackFrames,
                              methodName,
                              typeFullName,
                              state.StartTime,
                              null);

                // Uncomment when the native side of async probe method will be implemented
                // var snapshot = asyncState.SnapshotCreator.GetSnapshotJson();
                // LiveDebugger.Instance.AddSnapshot(snapshot);
            }
        }
    }
}
