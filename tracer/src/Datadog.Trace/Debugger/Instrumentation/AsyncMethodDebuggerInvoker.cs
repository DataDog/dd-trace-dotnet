// <copyright file="AsyncMethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
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
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncMethodDebuggerInvoker));

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
        /// <param name="kickoffInfo">The async kickoff method info</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AsyncMethodDebuggerState SetContext(AsyncHelper.AsyncKickoffMethodInfo kickoffInfo)
        {
            var currentState = AsyncContext.Value;
            var newState = new AsyncMethodDebuggerState
            {
                Parent = currentState,
                KickoffInvocationTarget = kickoffInfo.KickoffParentObject,
                KickoffInvocationTargetType = kickoffInfo.KickoffParentType,
                KickoffMethod = kickoffInfo.KickoffMethod
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
        /// <param name="isReEntryToMoveNext">If it the first entry to the state machine MoveNext method</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncMethodDebuggerState BeginMethod<TTarget>(string probeId, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex, ref bool isReEntryToMoveNext)
        {
            if (ProbeRateLimiter.Instance.IsLimitReached)
            {
                return AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }

            if (isReEntryToMoveNext)
            {
                return AsyncContext.Value;
            }

            isReEntryToMoveNext = true;
            var kickoffInfo = AsyncHelper.GetAsyncKickoffMethodInfo(instance);
            if (kickoffInfo.KickoffParentObject == null && kickoffInfo.KickoffMethod.IsStatic == false)
            {
                Log.Error($"{nameof(BeginMethod)}: hoisted 'this' has not found. {kickoffInfo.KickoffParentType.Name}.{kickoffInfo.KickoffMethod.Name}");
            }

            var asyncState = SetContext(kickoffInfo);

            if (!MethodMetadataProvider.TryCreateIfNotExists(instance, methodMetadataIndex, in methodHandle, in typeHandle, kickoffInfo))
            {
                Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadataId = {methodMetadataIndex}");
                return AsyncMethodDebuggerState.CreateInvalidatedDebuggerState();
            }

            // We are not in a continuation,
            // i.e. there is no context or the context just created in the kick off method but the asyncState machine not yet run
            // so create a new one and capture everything
            asyncState.ProbeId = probeId;
            asyncState.StartTime = DateTimeOffset.UtcNow;
            asyncState.MethodMetadataIndex = methodMetadataIndex;
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
            asyncState.SnapshotCreator.CaptureInstance(asyncState.KickoffInvocationTarget, asyncState.KickoffInvocationTargetType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BeginMethodLogArgs<TTarget>(TTarget instance, ref AsyncMethodDebuggerState asyncState)
        {
            // capture hoisted arguments
            // todo: should we save it in the state for later use?
            var kickOffMethodParameters = asyncState.KickoffMethod.GetParameters();
            var kickOffMethodArgumentsValues = AsyncHelper.GetHoistedArgumentsFromStateMachine(instance, kickOffMethodParameters);
            for (var index = 0; index < kickOffMethodArgumentsValues.Length; index++)
            {
                ref var parameter = ref kickOffMethodArgumentsValues[index];
                if (parameter == default)
                {
                    continue;
                }

                var parameterValue = parameter.Value;
                LogArg(ref parameterValue, parameter.Type, parameter.Name, index, ref asyncState);
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
        /// <param name="type">The type of the argument</param>
        /// <param name="paramName">The argument name</param>
        /// <param name="index">Index of given argument.</param>
        /// <param name="asyncState">Debugger asyncState</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogArg<TArg>(ref TArg arg, Type type, string paramName, int index, ref AsyncMethodDebuggerState asyncState)
        {
            asyncState.SnapshotCreator.CaptureArgument(arg, paramName, index == 0, asyncState.HasLocalsOrReturnValue, type);
            asyncState.HasLocalsOrReturnValue = false;
            asyncState.HasArguments = true;
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="asyncState">Debugger asyncState</param>
        /// https://datadoghq.atlassian.net/browse/DEBUG-928
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref AsyncMethodDebuggerState asyncState)
        {
            if (!asyncState.IsActive)
            {
                return;
            }

            var localNamesFromPdb = asyncState.MethodMetadataInfo.LocalVariableNames;
            if (!MethodDebuggerInvoker.TryGetLocalName(index, localNamesFromPdb, out var localName))
            {
                return;
            }

            asyncState.SnapshotCreator.CaptureLocal(local, localName, index == 0 && !asyncState.HasLocalsOrReturnValue);
            asyncState.HasLocalsOrReturnValue = true;
            asyncState.HasArguments = true;
        }

        /// <summary>
        /// End Method with Return value invoker - the MoveNext method always return void but here we send the kick-of method return value
        /// This method is called from either (1) the outer-most catch clause when the async method threw exception
        /// or (2) when the async method has logically ended.
        /// In this phase we have the correct async context in hand because we already set it in the BeginMethod.
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="asyncState">Debugger asyncState</param>
        /// <returns>LiveDebugger return structure</returns>
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance,  Exception exception, ref AsyncMethodDebuggerState asyncState)
        {
            if (!asyncState.IsActive)
            {
                return DebuggerReturn.GetDefault();
            }

            asyncState.SnapshotCreator.StartReturn();
            asyncState.SnapshotCreator.CaptureInstance(asyncState.KickoffInvocationTarget, asyncState.KickoffInvocationTargetType);
            if (exception != null)
            {
                asyncState.SnapshotCreator.CaptureException(exception);
            }

            EndMethodLogLocals(ref asyncState);
            BeginMethodLogArgs(instance, ref asyncState);

            return DebuggerReturn.GetDefault();
        }

        /// <summary>
        /// End Method with Return value invoker - the MoveNext method always return void but here we send the kick-of method return value
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

            asyncState.SnapshotCreator.StartReturn();
            asyncState.SnapshotCreator.CaptureInstance(asyncState.KickoffInvocationTarget, asyncState.KickoffInvocationTargetType);
            if (exception != null)
            {
                asyncState.SnapshotCreator.CaptureException(exception);
            }
            else
            {
                asyncState.SnapshotCreator.CaptureLocal(returnValue, "@return", true);
                asyncState.HasLocalsOrReturnValue = true;
            }

            EndMethodLogLocals(ref asyncState);
            BeginMethodLogArgs(instance, ref asyncState);
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
                asyncState.SnapshotCreator.CaptureLocal(local.Field.GetValue(asyncState.MoveNextInvocationTarget), local.SanitizedName, index == 0 && !asyncState.HasLocalsOrReturnValue, local.Field.FieldType);
                asyncState.HasLocalsOrReturnValue = true;
                asyncState.HasArguments = false;
            }
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
                                       asyncState.HasArguments;
            asyncState.SnapshotCreator.MethodProbeEndReturn(hasArgumentsOrLocals);
            FinalizeSnapshot(ref asyncState);
            RestoreContext();
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="exception">Exception instance</param>
        /// <param name="asyncState">Debugger asyncState</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException<TTarget>(Exception exception, ref AsyncMethodDebuggerState asyncState)
        {
            Log.Error(exception, "Error caused by our instrumentation");
            asyncState.IsActive = false;
        }

        /// <summary>
        /// Finalize snapshot
        /// </summary>
        /// <param name="asyncState">The async debugger asyncState</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FinalizeSnapshot(ref AsyncMethodDebuggerState asyncState)
        {
            using (asyncState.SnapshotCreator)
            {
                var stackFrames = new StackTrace(skipFrames: 2, true).GetFrames();
                var methodName = asyncState.MethodMetadataInfo.Method?.Name;
                var typeFullName = asyncState.MethodMetadataInfo.DeclaringType?.FullName;

                asyncState.SnapshotCreator.AddMethodProbeInfo(
                          asyncState.ProbeId,
                          methodName,
                          typeFullName)
                     .FinalizeSnapshot(
                              stackFrames,
                              methodName,
                              typeFullName,
                              asyncState.StartTime,
                              null);

                var snapshot = asyncState.SnapshotCreator.GetSnapshotJson();
                LiveDebugger.Instance.AddSnapshot(snapshot);
            }
        }
    }
}
