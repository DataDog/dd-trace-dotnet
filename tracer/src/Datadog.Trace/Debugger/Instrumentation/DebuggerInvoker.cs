// <copyright file="DebuggerInvoker.cs" company="Datadog">
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
    /// LiveDebugger Invoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerInvoker));
        private static readonly ImmutableDebuggerSettings Settings = ImmutableDebuggerSettings.Create(DebuggerSettings.FromDefaultSource());

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerState BeginMethod_StartMarker<TTarget>(TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex)
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

            var state = new DebuggerState(scope: default, DateTimeOffset.UtcNow, methodMetadataIndex);
            state.SnapshotCreator.StartDebugger();

            state.SnapshotCreator.StartSnapshot();
            state.SnapshotCreator.StartCapture();
            state.SnapshotCreator.StartEntry();
            state.SnapshotCreator.CaptureInstance(instance, state.MethodMetadaInfo.DeclaringType);
            return state;
        }

        private static DebuggerState CreateInvalidatedDebuggerState()
        {
            var defaultState = DebuggerState.GetDefault();
            defaultState.IsActive = false;
            return defaultState;
        }

        /// <summary>
        /// Ends the markering of BeginMethod.
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginMethod_EndMarker(ref DebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            state.HasLocalsOrReturnValue = false;
            state.SnapshotCreator.EndEntry();
        }

        /// <summary>
        /// Logs the given <paramref name="arg"/> ByRef.
        /// </summary>
        /// <typeparam name="TArg">Type of argument.</typeparam>
        /// <param name="arg">The argument to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(ref TArg arg, int index, ref DebuggerState state)
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
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref DebuggerState state)
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
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref DebuggerState state)
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
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref DebuggerState state)
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
        public static void EndMethod_EndMarker(ref DebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            var duration = DateTimeOffset.UtcNow - state.StartTime;
            state.SnapshotCreator.EndReturn();
            FinalizeAndUploadSnapshot(ref state, duration);
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException<TTarget>(Exception exception, ref DebuggerState state)
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

        private static Task FinalizeAndUploadSnapshot(ref DebuggerState state, TimeSpan? duration)
        {
            using (state.SnapshotCreator)
            {
                var frames = new StackTrace(skipFrames: 2, true).GetFrames() ?? Array.Empty<StackFrame>();
                MethodBase method = null;
                if (frames.Length > 0)
                {
                    method = frames[0]?.GetMethod();
                }

                // todo: should come from probe definition id
                var probeId = Guid.Empty.ToString();
                var methodName = method?.Name;
                var type = method?.DeclaringType?.FullName;

                state.SnapshotCreator
                     .AddProbeInfo(probeId, methodName, type)
                     .AddStackInfo(frames)
                     .EndSnapshot()
                     .EndDebugger()
                     .AddLoggerInfo(methodName, type)
                     .AddGeneralInfo(duration, Settings.ServiceName, null, null) // todo
                    ;

                var json = state.SnapshotCreator.GetSnapshotJson();
                return LiveDebugger.Instance.UploadSnapshot(json);
            }
        }
    }
}
