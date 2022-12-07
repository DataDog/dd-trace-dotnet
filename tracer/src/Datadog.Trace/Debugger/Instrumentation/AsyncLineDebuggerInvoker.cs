// <copyright file="AsyncLineDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// LineDebuggerInvoker Invoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AsyncLineDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LineDebuggerInvoker));

        private static AsyncLineDebuggerState CreateInvalidatedAsyncLineDebuggerState()
        {
            var defaultState = AsyncLineDebuggerState.GetDefault();
            defaultState.IsActive = false;
            return defaultState;
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref AsyncLineDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    return;
                }

                var localVariableNames = state.MethodMetadataInfo.LocalVariableNames;
                if (!MethodDebuggerInvoker.TryGetLocalName(index, localVariableNames, out var localName))
                {
                    return;
                }

                state.SnapshotCreator.CaptureLocal(local, localName);
                state.HasLocalsOrReturnValue = true;
            }
            catch (Exception e)
            {
                LogException(e, ref state);
            }
        }

        /// <summary>
        /// Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref AsyncLineDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    // Already encountered `LogException`
                    return;
                }

                Log.Warning(exception, "Error caused by our instrumentation");
                state.IsActive = false;
            }
            catch
            {
                // ignored
            }
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
        /// <param name="methodMetadataIndex">The index used to lookup the <see cref="MethodMetadataInfo"/> associated with the executing method</param>
        /// <param name="lineNumber">The line instrumented</param>
        /// <param name="probeFilePath">The path to the file of the probe</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncLineDebuggerState BeginLine<TTarget>(string probeId, TTarget instance, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, int methodMetadataIndex, int lineNumber, string probeFilePath)
        {
            try
            {
                if (!ProbeRateLimiter.Instance.Sample(probeId))
                {
                    return CreateInvalidatedAsyncLineDebuggerState();
                }

                if (instance == null)
                {
                    // Should not happen, but placing it here for a safeguard.
                    // We may load `instance` (aka `this`) as null when we deal with complex stack-allocated values types that are generic (either open/close generics).
                    // In the native side we already have a safeguard that will fail to instrument a method if we deal with such situations but I also placed a safeguard in the managed side just in case.
                    return CreateInvalidatedAsyncLineDebuggerState();
                }

                // Assess if we have metadata associated with the given index
                if (!MethodMetadataProvider.IsIndexExists(methodMetadataIndex))
                {
                    // State machine is null when we run in Optimized code and the original async method was generic,
                    // in which case the state machine is a generic value type.

                    var stateMachineType = instance.GetType();
                    var kickoffInfo = AsyncHelper.GetAsyncKickoffMethodInfo(instance, stateMachineType);
                    if (kickoffInfo.KickoffParentObject == null && kickoffInfo.KickoffMethod.IsStatic == false)
                    {
                        Log.Warning($"{nameof(BeginLine)}: hoisted 'this' has not found. {kickoffInfo.KickoffParentType.Name}.{kickoffInfo.KickoffMethod.Name}");
                    }

                    if (!MethodMetadataProvider.TryCreateAsyncMethodMetadataIfNotExists(instance, methodMetadataIndex, in methodHandle, in typeHandle, kickoffInfo))
                    {
                        Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadataId = {methodMetadataIndex}");
                        return CreateInvalidatedAsyncLineDebuggerState();
                    }
                }

                var kickoffParentObject = AsyncHelper.GetAsyncKickoffThisObject(instance);
                var state = new AsyncLineDebuggerState(probeId, scope: default, DateTimeOffset.UtcNow, methodMetadataIndex, lineNumber, probeFilePath, instance, kickoffParentObject);
                state.SnapshotCreator.StartDebugger();
                state.SnapshotCreator.StartSnapshot();
                state.SnapshotCreator.StartCaptures();
                state.SnapshotCreator.StartLines(lineNumber);

                return state;
            }
            catch (Exception e)
            {
                var invalidState = new AsyncLineDebuggerState();
                LogException(e, ref invalidState);
                return invalidState;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CollectArgs(ref AsyncLineDebuggerState asyncState)
        {
            // capture hoisted arguments
            var kickOffMethodArguments = asyncState.MethodMetadataInfo.AsyncMethodHoistedArguments;
            for (var index = 0; index < kickOffMethodArguments.Length; index++)
            {
                ref var argument = ref kickOffMethodArguments[index];
                if (argument == default)
                {
                    continue;
                }

                var argumentValue = argument.GetValue(asyncState.MoveNextInvocationTarget);
                LogArg(ref argumentValue, argument.FieldType, argument.Name, index, ref asyncState);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CollectLocals(ref AsyncLineDebuggerState asyncState)
        {
            // In the async scenario MethodMetadataInfo stores locals from MoveNext's localVarSig,
            // which isn't enough because we need to extract more locals that may be hoisted in the builder object
            // and we need to remove some locals that exist in the localVarSig but are just part of the async machinery and do not represent actual variables in the user's code.
            var kickOffMethodLocalsValues = asyncState.MethodMetadataInfo.AsyncMethodHoistedLocals;
            for (var index = 0; index < kickOffMethodLocalsValues.Length; index++)
            {
                ref var local = ref kickOffMethodLocalsValues[index];
                if (local == default)
                {
                    continue;
                }

                var localValue = local.Field.GetValue(asyncState.MoveNextInvocationTarget);
                LogLocal(ref localValue, local.Field.FieldType, local.SanitizedName, index, ref asyncState);
            }
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
        private static void LogArg<TArg>(ref TArg arg, Type type, string paramName, int index, ref AsyncLineDebuggerState asyncState)
        {
            asyncState.SnapshotCreator.CaptureArgument(arg, paramName, type);
            asyncState.HasLocalsOrReturnValue = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogLocal<TLocal>(ref TLocal local, Type type, string localName, int index, ref AsyncLineDebuggerState asyncState)
        {
            asyncState.SnapshotCreator.CaptureLocal(local, localName, type);
            asyncState.HasLocalsOrReturnValue = true;
        }

        /// <summary>
        /// End Line
        /// </summary>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndLine(ref AsyncLineDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    return;
                }

                var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                           state.MethodMetadataInfo.AsyncMethodHoistedArguments.Length > 0 ||
                                           state.KickoffInvocationTarget != null;
                state.HasLocalsOrReturnValue = false;
                CollectLocals(ref state);
                state.SnapshotCreator.CaptureInstance(state.KickoffInvocationTarget, state.MethodMetadataInfo.KickoffInvocationTargetType);
                CollectArgs(ref state);
                state.SnapshotCreator.CaptureStaticFields(state.MethodMetadataInfo.DeclaringType);
                state.SnapshotCreator.LineProbeEndReturn(hasArgumentsOrLocals);
                FinalizeSnapshot(ref state);
            }
            catch (Exception e)
            {
                LogException(e, ref state);
            }
        }

        private static void FinalizeSnapshot(ref AsyncLineDebuggerState state)
        {
            using (state.SnapshotCreator)
            {
                var stackFrames = new StackTrace(skipFrames: 2, true).GetFrames();
                var methodName = state.MethodMetadataInfo.KickoffMethod?.Name;
                var typeFullName = state.MethodMetadataInfo.KickoffInvocationTargetType?.FullName;

                state.SnapshotCreator.AddLineProbeInfo(
                          state.ProbeId,
                          state.ProbeFilePath,
                          state.LineNumber)
                     .FinalizeSnapshot(
                          stackFrames,
                          methodName,
                          typeFullName,
                          state.StartTime,
                          state.ProbeFilePath);

                var snapshot = state.SnapshotCreator.GetSnapshotJson();
                LiveDebugger.Instance.AddSnapshot(snapshot);
            }
        }
    }
}
