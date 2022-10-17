// <copyright file="MethodDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Conditions;
using Datadog.Trace.Debugger.RateLimiting;
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
            if (!ProbeRateLimiter.Instance.Sample(probeId))
            {
                return CreateInvalidatedDebuggerState();
            }

            if (!MethodMetadataProvider.TryCreateNonAsyncMethodMetadataIfNotExists(methodMetadataIndex, in methodHandle, in typeHandle))
            {
                Log.Warning($"BeginMethod_StartMarker: Failed to receive the InstrumentedMethodInfo associated with the executing method. type = {typeof(TTarget)}, instance type name = {instance?.GetType().Name}, methodMetadaId = {methodMetadataIndex}");
                return CreateInvalidatedDebuggerState();
            }

            var state = new MethodDebuggerState(probeId, scope: default, DateTimeOffset.UtcNow, methodMetadataIndex, instance, new ProbeCondition(" ", MethodPhase.Entry, " "));

            if (state.Condition != null)
            {
                state.Condition.SetShouldEvaluate(MethodPhase.Entry);
                return state;
            }

            BeginMethodStartMarkerCapture(state);
            return state;
        }

        private static void BeginMethodStartMarkerCapture(MethodDebuggerState state)
        {
            state.SnapshotCreator.StartDebugger();
            state.SnapshotCreator.StartSnapshot();
            state.SnapshotCreator.StartCaptures();
            state.SnapshotCreator.StartEntry();
            state.SnapshotCreator.CaptureStaticFields(state.MethodMetadataInfo.DeclaringType);
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
            if (!state.IsActive || state.Condition?.IsEndMethodCondition == true)
            {
                return;
            }

            if (state.Condition?.ShouldEvaluate == true)
            {
                state.Condition.SetInvocationTarget("this", state.MethodMetadataInfo.DeclaringType, state.InvocationTarget);
                var result = state.Condition.Evaluate();
                if (!result)
                {
                    state.IsActive = false;
                    return;
                }

                BeginMethodStartMarkerCapture(state);

                foreach (var member in state.Condition.ScopeMembers)
                {
                    switch (member.ElementType)
                    {
                        case ScopeMember.MemberType.Argument:
                        {
                            state.SnapshotCreator.CaptureArgument(member.Value, member.Name, member.Type);
                            break;
                        }

                        case ScopeMember.MemberType.Local:
                        case ScopeMember.MemberType.Return:
                        {
                            state.SnapshotCreator.CaptureLocal(member.Value, member.Name, member.Type);
                            break;
                        }

                        case ScopeMember.MemberType.Exception:
                        {
                            state.SnapshotCreator.CaptureException((Exception)member.Value);
                            break;
                        }
                    }
                }
            }

            state.SnapshotCreator.CaptureInstance(state.InvocationTarget, state.MethodMetadataInfo.DeclaringType);

            var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                       state.MethodMetadataInfo.ParameterNames.Length > 0 ||
                                       !state.MethodMetadataInfo.Method.IsStatic;
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

            var paramName = state.MethodMetadataInfo.ParameterNames[index];
            if (state.Condition?.ShouldEvaluate == true)
            {
                state.Condition.AddArgument(paramName, typeof(TArg), arg);
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

            if (state.Condition?.ShouldEvaluate == true)
            {
                state.Condition.AddLocal(localName, typeof(TLocal), local);
                return;
            }

            state.SnapshotCreator.CaptureLocal(local, localName);
            state.HasLocalsOrReturnValue = true;
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

            state.Condition?.SetShouldEvaluate(MethodPhase.End);

            if (state.Condition?.ShouldEvaluate == true)
            {
                state.Condition.AddException("exception", exception.GetType(), exception);
                return DebuggerReturn.GetDefault();
            }

            EndMethodStartMarkerCapture<object>(null, exception, ref state);

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

            state.Condition?.SetShouldEvaluate(MethodPhase.End);

            if (state.Condition?.ShouldEvaluate == true)
            {
                if (exception != null)
                {
                    state.Condition.AddException("exception", exception.GetType(), exception);
                }
                else
                {
                    state.Condition.AddReturn("return", typeof(TReturn), returnValue);
                }

                return new DebuggerReturn<TReturn>(returnValue);
            }

            EndMethodStartMarkerCapture(returnValue, exception, ref state);

            return new DebuggerReturn<TReturn>(returnValue);
        }

        private static void EndMethodStartMarkerCapture<TReturn>(TReturn returnValue, Exception exception, ref MethodDebuggerState state)
        {
            state.SnapshotCreator.StartReturn();
            state.SnapshotCreator.CaptureStaticFields(state.MethodMetadataInfo.DeclaringType);
            if (exception != null)
            {
                state.SnapshotCreator.CaptureException(exception);
            }
            else
            {
                state.SnapshotCreator.CaptureLocal(returnValue, "@return");
                state.HasLocalsOrReturnValue = true;
            }
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

            if (state.Condition?.ShouldEvaluate == true)
            {
                state.Condition.SetInvocationTarget("this", state.MethodMetadataInfo.DeclaringType, state.InvocationTarget);
                var result = state.Condition.Evaluate();
                if (!result)
                {
                    state.IsActive = false;
                    return;
                }

                EndMethodStartMarkerCapture(
                    state.Condition?.ScopeMembers.SingleOrDefault(m => m.ElementType == ScopeMember.MemberType.Return).Value,
                    (Exception)state.Condition?.ScopeMembers.SingleOrDefault(m => m.ElementType == ScopeMember.MemberType.Exception).Value,
                    ref state);
            }

            state.SnapshotCreator.CaptureInstance(state.InvocationTarget, state.MethodMetadataInfo.DeclaringType);

            var hasArgumentsOrLocals = state.HasLocalsOrReturnValue ||
                                       state.MethodMetadataInfo.ParameterNames.Length > 0 ||
                                       !state.MethodMetadataInfo.Method.IsStatic;
            state.SnapshotCreator.MethodProbeEndReturn(hasArgumentsOrLocals);
            FinalizeSnapshot(ref state);
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
        private static void FinalizeSnapshot(ref MethodDebuggerState state)
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
