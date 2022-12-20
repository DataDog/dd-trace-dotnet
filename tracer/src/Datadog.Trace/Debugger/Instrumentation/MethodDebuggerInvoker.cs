// <copyright file="MethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Conditions;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// MethodDebuggerInvoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static unsafe class MethodDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodDebuggerInvoker));

        private static readonly delegate* managed<ref MethodDebuggerState, void> FinalizeSnapshotFuncPointer = &FinalizeSnapshot;

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
            if (!MethodMetadataProvider.TryCreateNonAsyncMethodMetadataIfNotExists(methodMetadataIndex, in methodHandle, in typeHandle))
            {
                Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadaId = {methodMetadataIndex}");
                return CreateInvalidatedDebuggerState();
            }

            var state = new MethodDebuggerState(probeId, scope: default, DateTimeOffset.UtcNow, methodMetadataIndex, instance);
            if (ProbeExpressionsProcessor.Instance.HasExpression(probeId, ref state))
            {
                state.SnapshotCreator.Initialize();
                return state;
            }

            if (!ProbeRateLimiter.Instance.Sample(probeId))
            {
                return CreateInvalidatedDebuggerState();
            }

            state.SnapshotCreator.Initialize();
            state.SnapshotCreator.CaptureEntryMethodStartMarker(ref state);
            return state;
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

            if (!ProbeExpressionsProcessor.Instance.Process(ref state))
            {
                state.SnapshotCreator.CaptureEntryMethodEndMarker(ref state);
            }
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

            var paramName = state.MethodMetadataInfo.ParameterNames[index];

            if (ProbeExpressionsProcessor.Instance.AddMemberIfNeeded(ref state, paramName, typeof(TArg), arg, ScopeMemberKind.Argument))
            {
                return;
            }

            state.SnapshotCreator.CaptureArgument(arg, paramName);
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

            var localVariableNames = state.MethodMetadataInfo.LocalVariableNames;
            if (!TryGetLocalName(index, localVariableNames, out var localName))
            {
                return;
            }

            if (ProbeExpressionsProcessor.Instance.AddMemberIfNeeded(ref state, localName, typeof(TLocal), local, ScopeMemberKind.Local))
            {
                return;
            }

            state.SnapshotCreator.CaptureLocal(local, localName);
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

            state.MethodPhase = EvaluateAt.Exit;

            if (ProbeExpressionsProcessor.Instance.AddMemberIfNeeded(ref state, "exception", exception.GetType(), exception, ScopeMemberKind.Exception))
            {
                return DebuggerReturn.GetDefault();
            }

            state.SnapshotCreator.CaptureExitMethodStartMarker<object>(null, exception, ref state);
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

            state.MethodPhase = EvaluateAt.Exit;

            if (exception != null &&
                ProbeExpressionsProcessor.Instance.AddMemberIfNeeded(ref state, "exception", exception.GetType(), exception, ScopeMemberKind.Exception))
            {
                return new DebuggerReturn<TReturn>(returnValue);
            }

            if (ProbeExpressionsProcessor.Instance.AddMemberIfNeeded(ref state, "return", typeof(TReturn), returnValue, ScopeMemberKind.Return))
            {
                return new DebuggerReturn<TReturn>(returnValue);
            }

            state.SnapshotCreator.CaptureExitMethodStartMarker(returnValue, exception, ref state);
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

            if (!ProbeExpressionsProcessor.Instance.Process(ref state, FinalizeSnapshotFuncPointer))
            {
                state.SnapshotCreator.CaptureExitMethodEndMarker(ref state, FinalizeSnapshotFuncPointer);
            }
        }

        private static MethodDebuggerState CreateInvalidatedDebuggerState()
        {
            var defaultState = MethodDebuggerState.GetDefault();
            defaultState.IsActive = false;
            return defaultState;
        }

        internal static bool TryGetLocalName(int index, string[] localNamesFromPdb, out string localName)
        {
            localName = null;
            if (localNamesFromPdb != null)
            {
                if (index >= localNamesFromPdb.Length)
                {
                    // This is an extra local that does not appear in the PDB. This should only happen if the customer
                    // is using an IL weaving or obfuscation tool that neglects to update the PDB.
                    // There's nothing we can do, so let's just ignore it.
                    return false;
                }

                if (localNamesFromPdb[index] == null)
                {
                    // If the local does not appear in the PDB, then it is a compiler generated local and we shouldn't capture it.
                    return false;
                }
            }

            localName = localNamesFromPdb?[index] ?? "local_" + index;
            return true;
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref MethodDebuggerState state)
        {
            if (!state.IsActive)
            {
                // Already encountered `LogException`
                return;
            }

            Log.Warning(exception, "Error caused by our instrumentation");
            state.IsActive = false;
        }

        /// <summary>
        /// Gets the default value of a type
        /// </summary>
        /// <typeparam name="T">Type to get the default value</typeparam>
        /// <returns>Default value of T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetDefaultValue<T>() => default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FinalizeSnapshot(ref MethodDebuggerState state)
        {
            using (state.SnapshotCreator)
            {
                var stackFrames = new StackTrace(skipFrames: 2, true).GetFrames();
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

                var snapshot = state.SnapshotCreator.GetSnapshotJson();
                LiveDebugger.Instance.AddSnapshot(snapshot);
            }
        }
    }
}
