// <copyright file="AsyncMethodDebuggerInvoker.MultiProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Instrumentation.Collections;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// AsyncMethodDebuggerInvoker for multiple probes scenario (where there are more than one _method_ probe).
    /// </summary>
    public static partial class AsyncMethodDebuggerInvoker
    {
        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="instrumentationVersion">The version of this particular instrumentation.</param>
        /// <param name="isReEntryToMoveNext">If it the first entry to the state machine MoveNext method</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginMethod<TTarget>(TTarget instance, int methodMetadataIndex, int instrumentationVersion, ref AsyncDebuggerState isReEntryToMoveNext)
        {
            if (!MethodMetadataCollection.Instance.IndexExists(methodMetadataIndex))
            {
                Log.Warning("BeginMethod: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {Type}, instance type name = {Name}, methodMetadaId = {MethodMetadataIndex}, instrumentationVersion = {InstrumentationVersion}", new object[] { typeof(TTarget), instance?.GetType().Name, methodMetadataIndex, instrumentationVersion });
                isReEntryToMoveNext.LogStates = AsyncMethodDebuggerState.CreateInvalidatedDebuggerStates();
                return;
            }

            // State machine is null in case is a nested struct inside a generic parent.
            // This can happen if we operate in optimized code and the original async method was inside a generic class
            // or in case the original async method was generic, in which case the state machine is a generic value type
            // See more here: https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/method_rewriter.cpp#L70
            if (instance == null)
            {
                isReEntryToMoveNext.LogStates = AsyncMethodDebuggerState.CreateInvalidatedDebuggerStates();
                return;
            }

            ref var methodMetadataInfo = ref MethodMetadataCollection.Instance.Get(methodMetadataIndex);
            var probesIds = methodMetadataInfo.ProbeIds;
            var probeMetadataIndices = methodMetadataInfo.ProbeMetadataIndices;

            if (instrumentationVersion != methodMetadataInfo.InstrumentationVersion)
            {
                Log.Warning("BeginMethod: The instrumentation version is different, received version: {InstrumentationVersion}, but the kept version is: {KeptVersionNumber}", new object[] { instrumentationVersion, methodMetadataInfo.InstrumentationVersion });
                isReEntryToMoveNext.LogStates = AsyncMethodDebuggerState.CreateInvalidatedDebuggerStates();
                return;
            }

            if (IsReEnter(ref isReEntryToMoveNext))
            {
                // we are in a continuation, return the current state
                return;
            }

            isReEntryToMoveNext.LogStates = InstrumentationAllocator.RentArray<AsyncMethodDebuggerState>(probesIds.Length);

            for (var stateIndex = 0; stateIndex < isReEntryToMoveNext.LogStates.Length; stateIndex++)
            {
                BeginMethod(instance, methodMetadataIndex, probeMetadataIndices[stateIndex], instrumentationVersion, probesIds[stateIndex], ref isReEntryToMoveNext.LogStates[stateIndex]);
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
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref AsyncDebuggerState state)
        {
            for (var i = 0; i < state.LogStates.Length; i++)
            {
                LogLocal(ref local, index, ref state.LogStates[i]);
            }
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
            DebuggerReturn ret = default;

            for (var i = 0; i < state.LogStates.Length; i++)
            {
                ret = EndMethod_StartMarker(instance, exception, ref state.LogStates[i]);
            }

            return ret;
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
            DebuggerReturn<TReturn> ret = default;

            for (var i = 0; i < state.LogStates.Length; i++)
            {
                ret = EndMethod_StartMarker(instance, returnValue, exception, ref state.LogStates[i]);
            }

            return ret;
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod_EndMarker(ref AsyncDebuggerState state)
        {
            for (var i = 0; i < state.LogStates.Length; i++)
            {
                EndMethod_EndMarker(ref state.LogStates[i]);
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
                for (var i = 0; i < state.LogStates.Length; i++)
                {
                    LogException(exception, ref state.LogStates[i]);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
