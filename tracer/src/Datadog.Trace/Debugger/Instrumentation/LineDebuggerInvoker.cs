// <copyright file="LineDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// LineDebuggerInvoker Invoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class LineDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LineDebuggerInvoker));

        private static LineDebuggerState CreateInvalidatedLineDebuggerState()
        {
            var defaultState = LineDebuggerState.GetDefault();
            defaultState.IsActive = false;
            return defaultState;
        }

        /// <summary>
        /// Logs the given <paramref name="arg"/> ByRef.
        /// </summary>
        /// <typeparam name="TArg">Type of argument.</typeparam>
        /// <param name="arg">The argument to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(ref TArg arg, int index, ref LineDebuggerState state)
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
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref LineDebuggerState state)
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
        /// Log exception
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException<TTarget>(Exception exception, ref LineDebuggerState state)
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

        /// <summary>
        /// Begin Line Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="instance">Instance value</param>
        /// <param name="methodHandle">The handle of the executing method</param>
        /// <param name="typeHandle">The handle of the type</param>
        /// <param name="methodMetadataIndex">The index used to lookup for the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="lineNumber">The line instrumented</param>
        /// <param name="probeFilePath">The path to the file of the probe</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LineDebuggerState BeginLine<TTarget>(string probeId, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex, int lineNumber, string probeFilePath)
        {
            if (!ProbeRateLimiter.Instance.Sample(probeId))
            {
                return CreateInvalidatedLineDebuggerState();
            }

            if (!MethodMetadataProvider.TryCreateIfNotExists(methodMetadataIndex, in methodHandle, in typeHandle))
            {
                Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadaId = {methodMetadataIndex}");
                return CreateInvalidatedLineDebuggerState();
            }

            var state = new LineDebuggerState(probeId, scope: default, DateTimeOffset.UtcNow, methodMetadataIndex, lineNumber, probeFilePath);
            state.SnapshotCreator.StartDebugger();
            state.SnapshotCreator.StartSnapshot();
            state.SnapshotCreator.StartCaptures();
            state.SnapshotCreator.StartLines(lineNumber);
            state.SnapshotCreator.CaptureInstance(instance, state.MethodMetadaInfo.DeclaringType);
            return state;
        }

        /// <summary>
        /// End Line
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndLine(ref LineDebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            var duration = DateTimeOffset.UtcNow - state.StartTime;
            state.SnapshotCreator.LineProbeEndReturn();
            FinalizeSnapshot(ref state, duration);
        }

        private static void FinalizeSnapshot(ref LineDebuggerState state, TimeSpan? duration)
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
                var probeFilePath = state.ProbeFilePath;
                var methodName = method?.Name;
                var type = method?.DeclaringType?.FullName;
                var lineNumber = state.LineNumber;

                state.SnapshotCreator
                     .AddLineProbeInfo(probeId, probeFilePath, lineNumber)
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
