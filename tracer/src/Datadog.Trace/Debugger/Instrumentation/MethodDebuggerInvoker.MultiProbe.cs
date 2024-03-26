// <copyright file="MethodDebuggerInvoker.MultiProbe.cs" company="Datadog">
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
    /// MethodDebuggerInvoker for multiple probes scenario (where there are more than one _method_ probe).
    /// </summary>
    public static partial class MethodDebuggerInvoker
    {
        /// <summary>
        /// Begin Method Invoker that accepts multiple probes
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="instrumentationVersion">The version of the instrumentation</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodDebuggerState[] BeginMethod_StartMarker<TTarget>(TTarget instance, int methodMetadataIndex, int instrumentationVersion)
        {
            if (!MethodMetadataCollection.Instance.IndexExists(methodMetadataIndex))
            {
                Log.Warning("BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {Type}, instance type name = {Name}, methodMetadaId = {MethodMetadataIndex}, instrumentationVersion = {InstrumentationVersion}", new object[] { typeof(TTarget), instance?.GetType().Name, methodMetadataIndex, instrumentationVersion });
                return CreateInvalidatedDebuggerStates();
            }

            ref var methodMetadataInfo = ref MethodMetadataCollection.Instance.Get(methodMetadataIndex);
            var probesIds = methodMetadataInfo.ProbeIds;
            var probeMetadataIndices = methodMetadataInfo.ProbeMetadataIndices;
            var states = InstrumentationAllocator.RentArray<MethodDebuggerState>(probesIds.Length);

            for (var stateIndex = 0; stateIndex < states.Length; stateIndex++)
            {
                states[stateIndex] = BeginMethod_StartMarker(instance, methodMetadataIndex, probeMetadataIndices[stateIndex], probesIds[stateIndex]);
            }

            return states;
        }

        private static MethodDebuggerState[] CreateInvalidatedDebuggerStates()
        {
            return MethodDebuggerState.DisabledStates;
        }

        /// <summary>
        /// Ends the markering of BeginMethod.
        /// </summary>
        /// <param name="states">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginMethod_EndMarker(ref MethodDebuggerState[] states)
        {
            for (var i = 0; i < states.Length; i++)
            {
                BeginMethod_EndMarker(ref states[i]);
            }
        }

        /// <summary>
        /// Logs the given <paramref name="arg"/> ByRef.
        /// </summary>
        /// <typeparam name="TArg">Type of argument.</typeparam>
        /// <param name="arg">The argument to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="states">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(ref TArg arg, int index, ref MethodDebuggerState[] states)
        {
            for (var i = 0; i < states.Length; i++)
            {
                LogArg(ref arg, index, ref states[i]);
            }
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="states">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref MethodDebuggerState[] states)
        {
            for (var i = 0; i < states.Length; i++)
            {
                LogLocal(ref local, index, ref states[i]);
            }
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="states">Debugger state</param>
        /// <returns>CallTarget return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref MethodDebuggerState[] states)
        {
            DebuggerReturn ret = default;

            for (var i = 0; i < states.Length; i++)
            {
                ret = EndMethod_StartMarker(instance, exception, ref states[i]);
            }

            return ret;
        }

        /// <summary>
        /// End Method with Return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="states">Debugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref MethodDebuggerState[] states)
        {
            DebuggerReturn<TReturn> ret = default;

            for (var i = 0; i < states.Length; i++)
            {
                ret = EndMethod_StartMarker(instance, returnValue, exception, ref states[i]);
            }

            return ret;
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <param name="states">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndMethod_EndMarker(ref MethodDebuggerState[] states)
        {
            for (var i = 0; i < states.Length; i++)
            {
                ref var state = ref states[i];
                EndMethod_EndMarker(ref state);
            }
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="states">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref MethodDebuggerState[] states)
        {
            try
            {
                for (var i = 0; i < states.Length; i++)
                {
                    ref var state = ref states[i];
                    LogException(exception, ref state);
                }
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Used to clean up resources. Called upon entering the instrumented code's finally block.
        /// </summary>
        /// <param name="methodMetadataIndex">The unique index of the method.</param>
        /// <param name="instrumentationVersion">The unique identifier of the instrumentation.</param>
        /// <param name="states">Debugger states</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose(int methodMetadataIndex, int instrumentationVersion, ref MethodDebuggerState[] states)
        {
            // Should clean up states array. E.g: If we retrieved it from an Object Pool, we should return it here.
            InstrumentationAllocator.ReturnArray(ref states);
        }
    }
}
