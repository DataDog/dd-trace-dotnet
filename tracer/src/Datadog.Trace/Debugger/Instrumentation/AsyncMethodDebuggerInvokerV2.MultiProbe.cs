// <copyright file="AsyncMethodDebuggerInvokerV2.MultiProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Shim of <see cref="AsyncMethodDebuggerInvoker"/> with object type as the state, instead of <see cref="AsyncDebuggerState"/>.
    /// To better capture enter/leave of async methods, we inject a field into user's StateMachine types.
    /// The injected field used to be <see cref="AsyncDebuggerState"/> that lives inside Datadog.Trace.
    /// When our managed loader fails/bail out in loading our assembly, the runtime won't be able to resolve that type
    /// resulting in TypeLoadException. To avoid that, we inject `System.Object` as the field instead.
    /// In this case, even if we end up not loading Datadog.Trace, nothing bad will happen.
    /// </summary>
    public static class AsyncMethodDebuggerInvokerV2
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AsyncMethodDebuggerInvokerV2));

        /// <summary>
        /// Determines if the instrumentation should call <see cref="AsyncMethodDebuggerInvoker.UpdateProbeInfo{T}"/>.
        /// </summary>
        /// <param name="methodMetadataIndex">The unique index of the method.</param>
        /// <param name="instrumentationVersion">The unique identifier of the instrumentation.</param>
        /// <param name="state">state that is used to determine if it's the first entry to the state machine MoveNext method</param>
        /// <returns>true if <see cref="AsyncMethodDebuggerInvoker.UpdateProbeInfo{T}"/> should be called, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldUpdateProbeInfo(int methodMetadataIndex, int instrumentationVersion, ref object state)
        {
            ref var asyncState = ref AsyncHelper.GetStateRef(ref state);
            return AsyncMethodDebuggerInvoker.ShouldUpdateProbeInfo(methodMetadataIndex, instrumentationVersion, ref asyncState);
        }

        /// <summary>
        /// Shim on top of <see cref="AsyncMethodDebuggerInvoker.UpdateProbeInfo{T}"/>.
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
            AsyncMethodDebuggerInvoker.UpdateProbeInfo(probeIds, probeMetadataIndices, instance, methodMetadataIndex, instrumentationVersion, methodHandle, typeHandle);
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target object of the method. Note that it could be typeof(object) and not a concrete type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="instrumentationVersion">The version of this particular instrumentation.</param>
        /// <param name="state">State that is sued to know if it's the first entry to the state machine MoveNext method</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginMethod<TTarget>(TTarget instance, int methodMetadataIndex, int instrumentationVersion, ref object state)
        {
            ref var asyncState = ref AsyncHelper.GetStateRef(ref state);
            AsyncMethodDebuggerInvoker.BeginMethod(instance, methodMetadataIndex, instrumentationVersion, ref asyncState);
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref object state)
        {
            ref var asyncState = ref AsyncHelper.GetStateRef(ref state);
            AsyncMethodDebuggerInvoker.LogLocal(ref local, index, ref asyncState);
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
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref object state)
        {
            ref var asyncState = ref AsyncHelper.GetStateRef(ref state);
            return AsyncMethodDebuggerInvoker.EndMethod_StartMarker(instance, exception, ref asyncState);
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
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref object state)
        {
            ref var asyncState = ref AsyncHelper.GetStateRef(ref state);
            return AsyncMethodDebuggerInvoker.EndMethod_StartMarker(instance, returnValue, exception, ref asyncState);
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod_EndMarker(ref object state)
        {
            ref var asyncState = ref AsyncHelper.GetStateRef(ref state);
            AsyncMethodDebuggerInvoker.EndMethod_EndMarker(ref asyncState);
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref object state)
        {
            ref var asyncState = ref AsyncHelper.GetStateRef(ref state);
            AsyncMethodDebuggerInvoker.LogException(exception, ref asyncState);
        }
    }
}
