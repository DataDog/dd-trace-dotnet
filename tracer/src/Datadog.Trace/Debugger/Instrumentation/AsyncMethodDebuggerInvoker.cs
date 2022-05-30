// <copyright file="AsyncMethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// AsyncMethodDebuggerInvoker responsible for the managed side async method instrumentation,
    /// e.g. create the state, capture values and handle exceptions
    /// </summary>
    public static class AsyncMethodDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncMethodDebuggerInvoker));
        private static readonly ImmutableDebuggerSettings Settings = ImmutableDebuggerSettings.Create(DebuggerSettings.FromDefaultSource());

        // We have two scenarios:
        // 1. async method that does not participate in any other async method in i.e. has not async parent: probe on only one async method
        // 2. async method that has ben called from another async method (async parent) i.e. probe on two async methods, that has a parent child relationship: Async1 call to Async2 and we have probes on both of them
        // 2.1. async method that participate in a recursive call so we enter the state machine more than once but in a different context
        // For scenario 1, in the BeginMethod, the context will be empty in the first entry and not empty but with the correct context in the continuation
        // For scenario 2, in the BeginMethod of Async1, the context will be empty in the first entry and with the correct context in the continuation
        // but for Async2, the context will not be empty in the first entry, but with the parent context - Async1 and we need to identify it and set the correct context
        // for the continuation, we will have the correct context.
        // For scenario 2.1 is the same as scenario 2 but we should note that we are in the same method but not in the same context - recursive call.

        // We use the AsyncLocal to store the debugger state of each async method i.e. StateMachine.MoveNext run
        // We want to have the same debugger state when reentry in same context but a different debugger state when the entry is in a different context
        private static readonly AsyncLocal<AsyncMethodDebuggerState> AsyncContext = new();

        /// <summary>
        /// Create a new context and save the parent.
        /// We calling this method from the async kick off method before the state machine is created
        /// So for each async state machine, this method will be called only once
        /// and the IsFirstEntry property of the AsyncMethodDebuggerState will be "true"
        /// then, in the first state machine step, we set this property to "false"
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">The instance</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncMethodDebuggerState SetContext<TTarget>(TTarget instance)
        {
            var currentState = AsyncContext.Value;
            var newState = new AsyncMethodDebuggerState { Parent = currentState, InvocationTarget = instance };
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

            // MethodMetadataInfo saves locals from localVarSig,
            // this isn't enough in async scenario because we need to extract more locals the may hoisted in the builder object
            // and we need to subtract some locals that exist in the localVarSig but they are not belongs to the kickoff method
            // see EndMethodLogLocals
            if (!MethodMetadataProvider.TryCreateIfNotExists(methodMetadataIndex, in methodHandle, in typeHandle))
            {
                Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadataId = {methodMetadataIndex}");
                return AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }

            // There is an async context,
            // it can be context of the current executing async method
            // or the context of the parent call (other async method or recursive call of same method)
            // Async1 -> Async2 or Async1 -> Async1.
            var asyncState = AsyncContext.Value;
            if (asyncState is { IsFirstEntry: false })
            {
                // We are in a continuation, and we are in the same context as we hold in the AsyncLocal,
                // return the current async state without capture things
                return asyncState;
            }

            // We are not in a continuation,
            // i.e. there is no context or the context just created in the kick off method but the state machine not yet run
            // so create a new one and capture everything
            asyncState.ProbeId = probeId;
            asyncState.StartTime = DateTimeOffset.UtcNow;
            asyncState.MethodMetadataIndex = methodMetadataIndex;
            asyncState.IsFirstEntry = false;

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
            if (asyncState.InvocationTarget == null)
            {
                // static method
                return;
            }

            asyncState.SnapshotCreator.CaptureInstance(asyncState.InvocationTarget, asyncState.InvocationTarget.GetType());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BeginMethodLogArgs<TTarget>(TTarget instance, ref AsyncMethodDebuggerState asyncState)
        {
            // capture hoisted arguments
            var kickOffMethodParameters = AsyncHelper.GetAsyncKickoffMethod(asyncState.MethodMetadataInfo.DeclaringType).GetParameters();
            var kickOffMethodArgumentsValues = AsyncHelper.GetKickOffMethodArgumentsFromStateMachine(instance, kickOffMethodParameters);
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
        /// <param name="paramName">dd</param>
        /// <param name="index">index of given argument.</param>
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref AsyncMethodDebuggerState state)
        {
            // this method is called from the most outer catch clause when the async method threw exception or when the async method ended
            // in this phase we have the correct async context in hand because we already set it in the BeginMethod
            // In case our instrumentation threw exception the state.IsActive field will return false
            if (!state.IsActive)
            {
                return;
            }

            var localNamesFromPdb = state.MethodMetadataInfo.LocalVariableNames;
            if (localNamesFromPdb != null)
            {
                if (index >= localNamesFromPdb.Length)
                {
                    // This is an extra local that does not appear in the PDB. This should only happen if the customer
                    // is using an IL weaving or obfuscation tool that neglects to update the PDB.
                    // There's nothing we can do, so let's just ignore it.
                    return;
                }

                if (localNamesFromPdb[index] == null)
                {
                    // If the local does not appear in the PDB, then it is a compiler generated local and we shouldn't capture it.
                    return;
                }
            }

            var localName = localNamesFromPdb?[index] ?? "local_" + index;
            state.SnapshotCreator.CaptureLocal(local, localName, index == 0 && !state.HasLocalsOrReturnValue);
            state.HasLocalsOrReturnValue = true;
            state.HasArguments = true;
        }

        /// <summary>
        /// End Method with Void return value invoker
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

            EndMethodLogLocals(instance, ref state);
            return DebuggerReturn.GetDefault();
        }

        /// <summary>
        /// End Method with Return value invoker - the MoveNext method always return void but here we send the kick-of method return value
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

            EndMethodLogLocals(instance, ref state);
            return new DebuggerReturn<TReturn>(returnValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EndMethodLogLocals<TTarget>(TTarget instance, ref AsyncMethodDebuggerState asyncState)
        {
            // capture hoisted locals
            var kickOffMethodLocalsValues = AsyncHelper.GetKickOffMethodLocalsFromStateMachine(instance, asyncState.MethodMetadataInfo.DeclaringType);
            var numOfLocals = 0;
            for (var index = 0; index < kickOffMethodLocalsValues.Length; index++)
            {
                ref var local = ref kickOffMethodLocalsValues[index];
                if (local == default)
                {
                    continue;
                }

                asyncState.SnapshotCreator.CaptureLocal(local.Value, local.Name, numOfLocals == 0 && !asyncState.HasLocalsOrReturnValue);
                asyncState.HasLocalsOrReturnValue = true;
                asyncState.HasArguments = false;
                numOfLocals++;
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
        /// <param name="asyncState">The async state</param>
        /// <param name="task1">the task</param>
        /// <param name="stackFrames">frames</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FinalizeSnapshot(ref AsyncMethodDebuggerState asyncState, Task task1, StackFrame[] stackFrames)
        {
            if (task1.IsCanceled || task1.IsFaulted)
            {
                return;
            }

            using (asyncState.SnapshotCreator)
            {
                MethodBase method = null;
                if (stackFrames.Length > 0)
                {
                    method = stackFrames[0]?.GetMethod();
                }

                var probeId = asyncState.ProbeId;
                var methodName = method?.Name;
                var type = method?.DeclaringType?.FullName;
                var duration = DateTimeOffset.UtcNow - asyncState.StartTime;

                asyncState.SnapshotCreator
                          .AddMethodProbeInfo(probeId, methodName, type)
                          .AddStackInfo(stackFrames)
                          .EndSnapshot(duration)
                          .EndDebugger()
                          .AddLoggerInfo(methodName, type)
                          .AddGeneralInfo(Settings.ServiceName, null, null) // todo
                          .AddMessage()
                          .Complete()
                    ;

                // TODO: uncomment when the native side of async probe method will be implemented
                // var snapshot = asyncState.SnapshotCreator.GetSnapshotJson();
                // LiveDebugger.Instance.AddSnapshot(snapshot);
            }
        }
    }
}
