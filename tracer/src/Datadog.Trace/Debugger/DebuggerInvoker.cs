// <copyright file="DebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger
{
    /// <summary>
    /// LiveDebugger Invoker
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerInvoker));

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerState BeginMethod_StartMarker<TTarget>(TTarget instance)
        {
            if (ProbeRateLimiter.Instance.IsLimitReached)
            {
                var defaultState = DebuggerState.GetDefault();
                defaultState.IsActive = false;
                return defaultState;
            }

            var state = new DebuggerState(scope: default, DateTimeOffset.UtcNow);
            state.SnapshotCreator.StartCapture();
            state.SnapshotCreator.StartEntry();
            state.SnapshotCreator.CaptureInstance(instance);
            return state;
        }

        /// <summary>
        /// Ends the markering of BeginMethod.
        /// </summary>
        /// <param name="state">The state we're working on.</param>
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
        /// Logs the given <paramref name="arg"/>.
        /// </summary>
        /// <typeparam name="TArg">Type of argument.</typeparam>
        /// <param name="arg">The argument to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">The method at work.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(TArg arg, int index, ref DebuggerState state)
        {
        }

        /// <summary>
        /// Logs the given <paramref name="arg"/> ByRef.
        /// </summary>
        /// <typeparam name="TArg">Type of argument.</typeparam>
        /// <param name="arg">The argument to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">The method at work.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(ref TArg arg, int index, ref DebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            state.SnapshotCreator.CaptureArgument(arg, "argument_" + index, index == 0, state.HasLocalsOrReturnValue);
            state.HasLocalsOrReturnValue = false;
        }

        /// <summary>
        /// Logs the given <paramref name="local"/>.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">The method at work.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(TLocal local, int index, ref DebuggerState state)
        {
        }

        /// <summary>
        /// Logs the given <paramref name="local"/> ByRef.
        /// </summary>
        /// <typeparam name="TLocal">Type of local.</typeparam>
        /// <param name="local">The local to be logged.</param>
        /// <param name="index">index of given argument.</param>
        /// <param name="state">The method at work.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref DebuggerState state)
        {
            if (!state.IsActive)
            {
                return;
            }

            state.SnapshotCreator.CaptureLocal(local, "local_" + index, index == 0 && !state.HasLocalsOrReturnValue);
            state.HasLocalsOrReturnValue = true;
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">LiveDebugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, DebuggerState state)
        {
            return DebuggerReturn.GetDefault();
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">CallTarget state</param>
        /// <returns>CallTarget return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn EndMethod_StartMarker<TTarget>(TTarget instance, Exception exception, ref DebuggerState state)
        {
            if (!state.IsActive)
            {
                return DebuggerReturn.GetDefault();
            }

            state.SnapshotCreator.StartReturn();
            state.SnapshotCreator.CaptureInstance(instance);
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
        /// <param name="state">LiveDebugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, DebuggerState state)
        {
            return new DebuggerReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// End Method with Return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">LiveDebugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndMethod_StartMarker<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref DebuggerState state)
        {
            if (!state.IsActive)
            {
                return new DebuggerReturn<TReturn>(returnValue);
            }

            state.SnapshotCreator.StartReturn();
            state.SnapshotCreator.CaptureInstance(instance);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException<TTarget>(Exception exception)
        {
            Log.Error(exception, "Error caused by our instrumentation");
            // Waiting for native support
            // state.IsActive = false;
        }

        /// <summary>
        /// Gets the default value of a type
        /// </summary>
        /// <typeparam name="T">Type to get the default value</typeparam>
        /// <returns>Default value of T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetDefaultValue<T>() => default;

        private static void FinalizeAndUploadSnapshot(ref DebuggerState state, TimeSpan? duration)
        {
            var frames = new StackTrace(skipFrames: 2).GetFrames() ?? Array.Empty<StackFrame>();
            state.SnapshotCreator.AddProbeInfo(Guid.NewGuid(), frames[0]);
            state.SnapshotCreator.AddStackInfo(frames);
            state.SnapshotCreator.AddThreadInfo();
            state.SnapshotCreator.AddGeneralInfo(snapshotId: Guid.NewGuid(), duration: duration.GetValueOrDefault(TimeSpan.Zero).Milliseconds);
            var json = state.SnapshotCreator.GetSnapshotJson();
            state.SnapshotCreator.Dispose();
            SnapshotUploader.Instance.Post(json);
        }
    }
}
