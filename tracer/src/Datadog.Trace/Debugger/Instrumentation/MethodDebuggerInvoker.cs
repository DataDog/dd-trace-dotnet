// <copyright file="MethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// MethodDebuggerInvoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class MethodDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodDebuggerInvoker));

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="instance">Instance value</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodDebuggerState BeginMethod_StartMarker<TTarget>(string probeId, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex)
        {
            if (ProbeRateLimiter.Instance.IsLimitReached)
            {
                return CreateInvalidatedDebuggerState();
            }

            if (!MethodMetadataProvider.TryCreateIfNotExists(methodMetadataIndex, in methodHandle, in typeHandle))
            {
                Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadaId = {methodMetadataIndex}");
                return CreateInvalidatedDebuggerState();
            }

            var state = new MethodDebuggerState(probeId, scope: default, DateTimeOffset.UtcNow, methodMetadataIndex);
            state.SnapshotCreator.StartDebugger();

            state.SnapshotCreator.StartSnapshot();
            state.SnapshotCreator.StartCaptures();
            state.SnapshotCreator.StartEntry();
            state.SnapshotCreator.CaptureInstance(instance, state.MethodMetadaInfo.DeclaringType);
            return state;
        }

        private static MethodDebuggerState CreateInvalidatedDebuggerState()
        {
            var defaultState = MethodDebuggerState.GetDefault();
            defaultState.IsActive = false;
            return defaultState;
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
                                       state.MethodMetadaInfo.ParameterNames.Length > 0;
            state.HasLocalsOrReturnValue = false;
            state.SnapshotCreator.EndEntry(hasArgumentsOrLocals);
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

            var paramName = state.MethodMetadaInfo.ParameterNames[index];
            state.SnapshotCreator.CaptureArgument(arg, paramName, index == 0, state.HasLocalsOrReturnValue);
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

            var localNamesFromPdb = state.MethodMetadaInfo.LocalVariableNames;
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

            string localName = localNamesFromPdb?[index] ?? "local_" + index;
            state.SnapshotCreator.CaptureLocal(local, localName, index == 0 && !state.HasLocalsOrReturnValue);
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

            state.SnapshotCreator.StartReturn();
            state.SnapshotCreator.CaptureInstance(instance, state.MethodMetadaInfo.DeclaringType);
            if (exception != null)
            {
                state.SnapshotCreator.CaptureException(exception);
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

            state.SnapshotCreator.StartReturn();
            state.SnapshotCreator.CaptureInstance(instance, state.MethodMetadaInfo.DeclaringType);
            if (exception != null)
            {
                state.SnapshotCreator.CaptureException(exception);
            }
            else
            {
                state.SnapshotCreator.CaptureLocal(returnValue, "@return", true);
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
                                       state.MethodMetadaInfo.ParameterNames.Length > 0;
            state.SnapshotCreator.MethodProbeEndReturn(hasArgumentsOrLocals);
            FinalizeSnapshot(ref state);
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException<TTarget>(Exception exception, ref MethodDebuggerState state)
        {
            Log.Error(exception, "Error caused by our instrumentation");
            state.IsActive = false;
        }

        /// <summary>
        /// Gets the default value of a type
        /// </summary>
        /// <typeparam name="T">Type to get the default value</typeparam>
        /// <returns>Default value of T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetDefaultValue<T>() => default;

        private static void FinalizeSnapshot(ref MethodDebuggerState state)
        {
            using (state.SnapshotCreator)
            {
                var frames = new StackTrace(skipFrames: 2, true).GetFrames() ?? Array.Empty<StackFrame>();
                MethodBase method = null;
                if (frames.Length > 0)
                {
                    method = frames[0]?.GetMethod();
                }

                var probeId = state.ProbeId;
                var methodName = method?.Name;
                var type = method?.DeclaringType?.FullName;
                var duration = DateTimeOffset.UtcNow - state.StartTime;

                state.SnapshotCreator
                     .AddMethodProbeInfo(probeId, methodName, type)
                     .AddStackInfo(frames)
                     .EndSnapshot(duration)
                     .EndDebugger()
                     .AddLoggerInfo(methodName, type)
                     .AddGeneralInfo(LiveDebugger.Instance.ServiceName, null, null) // todo
                     .AddMessage()
                    ;

                var snapshot = state.SnapshotCreator.GetSnapshotJson();
                LiveDebugger.Instance.AddSnapshot(snapshot);
            }
        }
    }
}
