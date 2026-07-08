// <copyright file="FlowRecorder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    /// <summary>
    /// Lightweight managed callback surface for the live debugger POC flow recorder.
    /// </summary>
    public static class FlowRecorder
    {
        private const int CachedValueNameCount = 32;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FlowRecorder));
        private static readonly object SyncRoot = new();
        private static readonly AsyncLocal<FlowRecorderOperationContext?> CurrentOperation = new();
        private static readonly string[] ArgumentNames = CreateIndexedValueNames("arg");
        private static readonly string[] LocalNames = CreateIndexedValueNames("local");

        [ThreadStatic]
        private static ulong _currentAsyncOperationId;
        [ThreadStatic]
        private static long _currentAsyncOperationGeneration;
        [ThreadStatic]
        private static long _currentFlowGeneration;
        [ThreadStatic]
        private static ulong _currentFlowId;
        [ThreadStatic]
        private static ulong _currentFrameId;
        [ThreadStatic]
        private static int _currentDepth;
        [ThreadStatic]
        private static ulong _currentOperationId;
        [ThreadStatic]
        private static long _currentOperationGeneration;

        private static RecorderSession? _session;
        private static long _generation;
        private static long _nextFlowId;
        private static long _nextFrameId;
        private static long _nextOperationId;
        private static long _droppedEvents;
        private static volatile bool _initialized;

        /// <summary>
        /// Gets the number of events dropped because the bounded recorder buffer was full.
        /// </summary>
        public static long DroppedEvents => Volatile.Read(ref _droppedEvents);

        /// <summary>
        /// Resets the recorder for tests and local POC runs.
        /// </summary>
        public static void Reset()
        {
            lock (SyncRoot)
            {
                var settings = FlowRecorderSettings.FromEnvironment();
                ClearThreadStaticState();
                var generation = ++_generation;
                _nextFlowId = 0;
                _nextFrameId = 0;
                _nextOperationId = 0;
                _droppedEvents = 0;
                Volatile.Write(ref _session, CreateSession(generation, settings, useWindowsGate: true));
                _initialized = true;
            }
        }

        /// <summary>
        /// Starts a recorder-local operation scope. This is the product gate and identity; trace ids are optional correlation.
        /// </summary>
        /// <param name="triggerReason">The reason recording was armed.</param>
        /// <param name="root">The logical root that matched.</param>
        /// <param name="traceIdUpper">Optional upper 64 bits of the trace id.</param>
        /// <param name="traceIdLower">Optional lower 64 bits of the trace id.</param>
        /// <param name="rootSpanId">Optional root span id.</param>
        /// <param name="activeSpanId">Optional active span id.</param>
        /// <returns>An operation scope that restores the previous operation on dispose.</returns>
        public static IDisposable StartOperation(
            string triggerReason,
            string root,
            ulong traceIdUpper = 0,
            ulong traceIdLower = 0,
            ulong rootSpanId = 0,
            ulong activeSpanId = 0)
        {
            if (!TryGetEnabledSession(out var session))
            {
                return FlowRecorderOperationScope.Noop;
            }

            if ((traceIdUpper | traceIdLower | rootSpanId | activeSpanId) == 0 && !session.Settings.SkipTraceCorrelation)
            {
                GetTraceCorrelation(out traceIdUpper, out traceIdLower, out rootSpanId, out activeSpanId);
            }

            var previousOperationId = _currentOperationId;
            var previousOperationGeneration = _currentOperationGeneration;
            var previousOperation = CurrentOperation.Value;
            var operationId = NextId(ref _nextOperationId);
            var operation = new FlowRecorderOperationContext(session.Generation, operationId, traceIdUpper, traceIdLower, rootSpanId, activeSpanId);
            _currentOperationId = operationId;
            _currentOperationGeneration = session.Generation;
            CurrentOperation.Value = operation;
            session.IncrementActiveOperationCount();
            session.Sink?.TryRegisterOperation(new FlowOperationMetadata(
                operationId,
                session.Generation,
                triggerReason ?? string.Empty,
                root ?? string.Empty,
                Stopwatch.GetTimestamp(),
                traceIdUpper,
                traceIdLower,
                rootSpanId,
                activeSpanId));
            return new FlowRecorderOperationScope(session, operation, previousOperationId, previousOperationGeneration, previousOperation);
        }

        /// <summary>
        /// Starts a recorder-local operation scope and flushes buffered data when the operation ends.
        /// </summary>
        /// <param name="triggerReason">The reason recording was armed.</param>
        /// <param name="root">The logical root that matched.</param>
        /// <param name="outputPath">The capture path to write when the operation ends.</param>
        /// <returns>An operation scope that restores the previous operation and flushes on dispose.</returns>
        public static IDisposable StartOperationAndFlushOnDispose(string triggerReason, string root, string outputPath)
        {
            var scope = StartOperation(triggerReason, root);
            return ReferenceEquals(scope, FlowRecorderOperationScope.Noop)
                       ? scope
                       : new FlushOnDisposeScope(scope, outputPath);
        }

        /// <summary>
        /// Starts an operation using the configured root metadata, falling back to supplied defaults.
        /// </summary>
        /// <param name="defaultTriggerReason">Fallback trigger reason.</param>
        /// <param name="defaultRoot">Fallback logical root.</param>
        /// <returns>An operation scope that restores the previous operation on dispose.</returns>
        public static IDisposable StartConfiguredOperation(string defaultTriggerReason, string defaultRoot)
        {
            var settings = GetOrCreateSession().Settings;
            return StartOperation(
                settings.TriggerReason ?? defaultTriggerReason,
                settings.Root ?? defaultRoot);
        }

        /// <summary>
        /// Starts a recorded method frame.
        /// </summary>
        /// <param name="methodMetadataIndex">The method metadata index assigned by native instrumentation.</param>
        /// <returns>Opaque state that must be passed to the exit callback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FlowRecorderState Enter(int methodMetadataIndex)
        {
            if (!TryGetEnabledSession(out var session) || !TryGetActiveOperation(session, out var operationContext, out var operationId))
            {
                return default;
            }

            ThrowIfFaultInjectionEnabled(session.Settings.ThrowOnEnter, "enter");

            var flowId = _currentFlowId;
            var parentFrameId = _currentFrameId;
            var parentDepth = _currentDepth;
            if (session.Settings.DisableFlowContext || _currentFlowGeneration != session.Generation || flowId == 0)
            {
                flowId = NextId(ref _nextFlowId);
                parentFrameId = 0;
                parentDepth = 0;
            }

            var frameId = NextId(ref _nextFrameId);
            var depth = parentDepth + 1;
            var state = new FlowRecorderState(session.Generation, operationContext, operationId, flowId, frameId, parentFrameId, depth, methodMetadataIndex);
            Enqueue(session, CreateEvent(FlowEventKind.Enter, methodMetadataIndex, operationId, flowId, frameId, parentFrameId, depth, exceptionTypeId: 0));
            if (!session.Settings.DisableFlowContext)
            {
                _currentFlowGeneration = session.Generation;
                _currentFlowId = flowId;
                _currentFrameId = frameId;
                _currentDepth = depth;
            }

            return state;
        }

        /// <summary>
        /// Starts a broad recorder frame using the event-only fast path.
        /// </summary>
        /// <param name="methodMetadataIndex">The method metadata index assigned by native instrumentation.</param>
        /// <returns>Opaque state that must be passed to the fast exit callback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FlowRecorderState EnterFast(int methodMetadataIndex)
        {
            if (!TryGetEnabledSession(out var session) || !TryGetActiveOperation(session, out var operationContext, out var operationId))
            {
                return default;
            }

            var flowId = _currentFlowId;
            var parentFrameId = _currentFrameId;
            var parentDepth = _currentDepth;
            if (session.Settings.DisableFlowContext || _currentFlowGeneration != session.Generation || flowId == 0)
            {
                flowId = NextId(ref _nextFlowId);
                parentFrameId = 0;
                parentDepth = 0;
            }

            var frameId = NextId(ref _nextFrameId);
            var depth = parentDepth + 1;
            var state = new FlowRecorderState(session.Generation, operationContext, operationId, flowId, frameId, parentFrameId, depth, methodMetadataIndex);
            Enqueue(session, CreateEvent(FlowEventKind.Enter, methodMetadataIndex, operationId, flowId, frameId, parentFrameId, depth, exceptionTypeId: 0));
            if (!session.Settings.DisableFlowContext)
            {
                _currentFlowGeneration = session.Generation;
                _currentFlowId = flowId;
                _currentFrameId = frameId;
                _currentDepth = depth;
            }

            return state;
        }

        /// <summary>
        /// Starts a recorded async MoveNext step using a stable async operation id stored on the state machine instance.
        /// </summary>
        /// <param name="methodMetadataIndex">The method metadata index assigned by native instrumentation.</param>
        /// <param name="operationId">State-machine field that identifies the logical async invocation.</param>
        /// <param name="generation">State-machine field that identifies the recorder generation for the operation id.</param>
        /// <returns>Opaque state that must be passed to the exit callback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FlowRecorderState EnterAsyncStep(int methodMetadataIndex, ref long operationId, ref long generation)
        {
            if (!TryGetEnabledSession(out var session) || !TryGetActiveOperation(session, out var operationContext, out var recorderOperationId))
            {
                return default;
            }

            ThrowIfFaultInjectionEnabled(session.Settings.ThrowOnEnter, "enter");

            var previousAsyncOperationId = GetCurrentAsyncOperationId(session.Generation);
            var previousAsyncOperationGeneration = previousAsyncOperationId == 0 ? 0 : session.Generation;
            var isNewOperation = operationId == 0 || generation != session.Generation;
            if (isNewOperation)
            {
                operationId = (long)NextId(ref _nextFlowId);
                generation = session.Generation;
            }

            var flowId = (ulong)operationId;
            var frameId = NextId(ref _nextFrameId);
            var state = new FlowRecorderState(session.Generation, operationContext, recorderOperationId, flowId, frameId, parentFrameId: 0, depth: 1, methodMetadataIndex, previousAsyncOperationId, previousAsyncOperationGeneration, restoreAsyncOperationId: true);
            if (isNewOperation && previousAsyncOperationId != 0)
            {
                Enqueue(session, CreateEvent(FlowEventKind.AsyncEdge, methodMetadataIndex, recorderOperationId, previousAsyncOperationId, flowId, parentFrameId: 0, depth: 0, exceptionTypeId: 0));
            }

            Enqueue(session, CreateEvent(FlowEventKind.Enter, methodMetadataIndex, recorderOperationId, flowId, frameId, parentFrameId: 0, depth: 1, exceptionTypeId: 0));
            _currentAsyncOperationId = flowId;
            _currentAsyncOperationGeneration = session.Generation;
            return state;
        }

        /// <summary>
        /// Starts a recorded async MoveNext step using the event-only fast path.
        /// </summary>
        /// <param name="methodMetadataIndex">The method metadata index assigned by native instrumentation.</param>
        /// <param name="operationId">State-machine field that identifies the logical async invocation.</param>
        /// <param name="generation">State-machine field that identifies the recorder generation for the operation id.</param>
        /// <returns>Opaque state that must be passed to the fast exit callback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FlowRecorderState EnterAsyncStepFast(int methodMetadataIndex, ref long operationId, ref long generation)
        {
            if (!TryGetEnabledSession(out var session) || !TryGetActiveOperation(session, out var operationContext, out var recorderOperationId))
            {
                return default;
            }

            var previousAsyncOperationId = GetCurrentAsyncOperationId(session.Generation);
            var previousAsyncOperationGeneration = previousAsyncOperationId == 0 ? 0 : session.Generation;
            var isNewOperation = operationId == 0 || generation != session.Generation;
            if (isNewOperation)
            {
                operationId = (long)NextId(ref _nextFlowId);
                generation = session.Generation;
            }

            var flowId = (ulong)operationId;
            var frameId = NextId(ref _nextFrameId);
            var state = new FlowRecorderState(session.Generation, operationContext, recorderOperationId, flowId, frameId, parentFrameId: 0, depth: 1, methodMetadataIndex, previousAsyncOperationId, previousAsyncOperationGeneration, restoreAsyncOperationId: true);
            if (isNewOperation && previousAsyncOperationId != 0)
            {
                Enqueue(session, CreateEvent(FlowEventKind.AsyncEdge, methodMetadataIndex, recorderOperationId, previousAsyncOperationId, flowId, parentFrameId: 0, depth: 0, exceptionTypeId: 0));
            }

            Enqueue(session, CreateEvent(FlowEventKind.Enter, methodMetadataIndex, recorderOperationId, flowId, frameId, parentFrameId: 0, depth: 1, exceptionTypeId: 0));
            _currentAsyncOperationId = flowId;
            _currentAsyncOperationGeneration = session.Generation;
            return state;
        }

        /// <summary>
        /// Records that the currently running async operation started another async operation.
        /// </summary>
        /// <param name="methodMetadataIndex">The method metadata index assigned to the async kickoff method.</param>
        /// <param name="childOperationId">State-machine field that identifies the child async invocation.</param>
        /// <param name="childGeneration">State-machine field that identifies the recorder generation for the child operation id.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordAsyncEdge(int methodMetadataIndex, ref long childOperationId, ref long childGeneration)
        {
            if (!TryGetEnabledSession(out var session) || !TryGetActiveOperation(session, out _, out var recorderOperationId))
            {
                return;
            }

            var parentOperationId = GetCurrentAsyncOperationId(session.Generation);
            if (parentOperationId == 0)
            {
                return;
            }

            var isNewOperation = childOperationId == 0 || childGeneration != session.Generation;
            if (isNewOperation)
            {
                childOperationId = (long)NextId(ref _nextFlowId);
                childGeneration = session.Generation;
            }

            if (isNewOperation)
            {
                Enqueue(session, CreateEvent(FlowEventKind.AsyncEdge, methodMetadataIndex, recorderOperationId, parentOperationId, (ulong)childOperationId, parentFrameId: 0, depth: 0, exceptionTypeId: 0));
            }
        }

        /// <summary>
        /// Registers a display name for a native recorder method metadata index.
        /// </summary>
        /// <param name="methodMetadataIndex">The method metadata index assigned by native instrumentation.</param>
        /// <param name="methodHandle">The runtime handle of the instrumented method.</param>
        /// <param name="typeHandle">The runtime handle of the instrumented type.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegisterMethod(int methodMetadataIndex, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle)
        {
            if (!TryGetEnabledSession(out var session))
            {
                return;
            }

            if (session.Settings.SkipMethodRegistration)
            {
                return;
            }

            session.Sink?.TryRegisterMethod(methodMetadataIndex, methodHandle, typeHandle);
        }

        /// <summary>
        /// Registers a display name for a recorder method metadata index.
        /// </summary>
        /// <param name="methodMetadataIndex">The recorder method metadata index.</param>
        /// <param name="displayName">The display name to write into the capture metadata table.</param>
        public static void RegisterMethodNameForTesting(int methodMetadataIndex, string displayName)
        {
            if (!TryGetEnabledSession(out var session))
            {
                return;
            }

            session.Sink?.TryRegisterMethodName(methodMetadataIndex, displayName);
        }

        /// <summary>
        /// Starts a sample-driven async operation with an explicit logical parent.
        /// </summary>
        /// <param name="methodMetadataIndex">The recorder method metadata index.</param>
        /// <param name="parentOperationId">The logical parent async operation id, or zero for a root operation.</param>
        /// <param name="operationId">State-machine-like field that identifies the logical async invocation.</param>
        /// <param name="generation">State-machine-like field that identifies the recorder generation for the operation id.</param>
        /// <returns>Opaque state that must be passed to the exit callback.</returns>
        public static FlowRecorderState EnterAsyncOperationForTesting(int methodMetadataIndex, long parentOperationId, ref long operationId, ref long generation)
        {
            if (!TryGetEnabledSession(out var session) || !TryGetActiveOperation(session, out var operationContext, out var recorderOperationId))
            {
                return default;
            }

            ThrowIfFaultInjectionEnabled(session.Settings.ThrowOnEnter, "enter");

            var isNewOperation = operationId == 0 || generation != session.Generation;
            if (isNewOperation)
            {
                operationId = (long)NextId(ref _nextFlowId);
                generation = session.Generation;
            }

            var flowId = (ulong)operationId;
            var frameId = NextId(ref _nextFrameId);
            if (isNewOperation && parentOperationId > 0)
            {
                Enqueue(session, CreateEvent(FlowEventKind.AsyncEdge, methodMetadataIndex, recorderOperationId, (ulong)parentOperationId, flowId, parentFrameId: 0, depth: 0, exceptionTypeId: 0));
            }

            var state = new FlowRecorderState(session.Generation, operationContext, recorderOperationId, flowId, frameId, parentFrameId: 0, depth: 1, methodMetadataIndex);
            Enqueue(session, CreateEvent(FlowEventKind.Enter, methodMetadataIndex, recorderOperationId, flowId, frameId, parentFrameId: 0, depth: 1, exceptionTypeId: 0));
            return state;
        }

        /// <summary>
        /// Starts a recorded method frame without publishing it to the flowing async context.
        /// </summary>
        /// <param name="methodMetadataIndex">The method metadata index assigned by native instrumentation.</param>
        /// <returns>Opaque state that must be passed to the exit callback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FlowRecorderState EnterDetached(int methodMetadataIndex)
        {
            if (!TryGetEnabledSession(out var session) || !TryGetActiveOperation(session, out var operationContext, out var operationId))
            {
                return default;
            }

            ThrowIfFaultInjectionEnabled(session.Settings.ThrowOnEnter, "enter");

            var flowId = NextId(ref _nextFlowId);
            var frameId = NextId(ref _nextFrameId);
            var state = new FlowRecorderState(session.Generation, operationContext, operationId, flowId, frameId, parentFrameId: 0, depth: 1, methodMetadataIndex);
            Enqueue(session, CreateEvent(FlowEventKind.Enter, methodMetadataIndex, operationId, flowId, frameId, parentFrameId: 0, depth: 1, exceptionTypeId: 0));
            return state;
        }

        /// <summary>
        /// Ends a recorded method frame.
        /// </summary>
        /// <param name="state">The state returned by <see cref="Enter"/>.</param>
        /// <param name="exception">The method exception, if one was thrown.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Exit(ref FlowRecorderState state, Exception? exception = null)
        {
            if (!state.IsValid)
            {
                return;
            }

            RestoreFrameContext(ref state);

            var session = Volatile.Read(ref _session);
            if (session?.Sink is null || state.Generation != session.Generation)
            {
                return;
            }

            if (exception is not null)
            {
                Enqueue(session, CreateEvent(FlowEventKind.Exception, state.MethodMetadataIndex, state.OperationId, state.FlowId, state.FrameId, state.ParentFrameId, state.Depth, RuntimeHelpers.GetHashCode(exception.GetType())));
                session.Sink.TryEnqueueExceptionDetails(state.FlowId, state.FrameId, exception, session.Settings);
            }

            Enqueue(session, CreateEvent(FlowEventKind.Exit, state.MethodMetadataIndex, state.OperationId, state.FlowId, state.FrameId, state.ParentFrameId, state.Depth, exceptionTypeId: 0));
            ThrowIfFaultInjectionEnabled(session.Settings.ThrowOnExit, "exit");
        }

        /// <summary>
        /// Ends a broad recorder frame using the event-only fast path.
        /// </summary>
        /// <param name="state">The state returned by <see cref="EnterFast"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExitFast(ref FlowRecorderState state)
        {
            if (!state.IsValid)
            {
                return;
            }

            RestoreFrameContext(ref state);

            var session = Volatile.Read(ref _session);
            if (session?.Sink is null || state.Generation != session.Generation)
            {
                return;
            }

            Enqueue(session, CreateEvent(FlowEventKind.Exit, state.MethodMetadataIndex, state.OperationId, state.FlowId, state.FrameId, state.ParentFrameId, state.Depth, exceptionTypeId: 0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ShouldCaptureValues(ref FlowRecorderState state, FlowCapturePhase phase)
        {
            return ShouldCaptureValues(ref state, (int)phase);
        }

        /// <summary>
        /// Determines whether native value-capable instrumentation should execute value callbacks for the requested phase.
        /// </summary>
        /// <param name="state">The active recorder frame state.</param>
        /// <param name="phase">The numeric <see cref="FlowCapturePhase"/> value.</param>
        /// <returns><c>true</c> when values should be captured for this frame and phase.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldCaptureValues(ref FlowRecorderState state, int phase)
        {
            if (!state.IsValid)
            {
                return false;
            }

            var session = Volatile.Read(ref _session);
            if (session?.Sink is null || state.Generation != session.Generation)
            {
                return false;
            }

            return ShouldCapturePhase(session.Settings.ValueCaptureMode, (FlowCapturePhase)phase);
        }

        /// <summary>
        /// Records an argument value for a value-capable recorder frame.
        /// </summary>
        /// <typeparam name="TArg">The argument type.</typeparam>
        /// <param name="arg">The argument value.</param>
        /// <param name="index">The argument index.</param>
        /// <param name="state">The active recorder frame state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogArg<TArg>(ref TArg arg, int index, ref FlowRecorderState state)
        {
            LogValue(ref arg, index, FlowCapturePhase.Entry, FlowValueKind.Argument, ref state);
        }

        /// <summary>
        /// Records a local value for a value-capable recorder frame.
        /// </summary>
        /// <typeparam name="TLocal">The local type.</typeparam>
        /// <param name="local">The local value.</param>
        /// <param name="index">The local index.</param>
        /// <param name="state">The active recorder frame state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogLocal<TLocal>(ref TLocal local, int index, ref FlowRecorderState state)
        {
            LogValue(ref local, index, FlowCapturePhase.Exit, FlowValueKind.Local, ref state);
        }

        /// <summary>
        /// Records a named field value for async state-machine frames.
        /// </summary>
        /// <typeparam name="TField">The field type.</typeparam>
        /// <param name="field">The field value.</param>
        /// <param name="name">The field name.</param>
        /// <param name="state">The active recorder frame state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogField<TField>(ref TField field, string name, ref FlowRecorderState state)
        {
            LogNamedValue(ref field, name, FlowCapturePhase.Exit, FlowValueKind.Local, ref state);
        }

        /// <summary>
        /// Records a named argument field for async state-machine frames.
        /// </summary>
        /// <typeparam name="TField">The field type.</typeparam>
        /// <param name="field">The field value.</param>
        /// <param name="name">The source argument name.</param>
        /// <param name="state">The active recorder frame state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogFieldArgument<TField>(ref TField field, string name, ref FlowRecorderState state)
        {
            LogNamedValue(ref field, name, FlowCapturePhase.Entry, FlowValueKind.Argument, ref state);
        }

        /// <summary>
        /// Records a return value for a value-capable recorder frame.
        /// </summary>
        /// <typeparam name="TReturn">The return type.</typeparam>
        /// <param name="value">The return value.</param>
        /// <param name="state">The active recorder frame state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogReturn<TReturn>(ref TReturn value, ref FlowRecorderState state)
        {
            LogValue(ref value, -1, FlowCapturePhase.Exit, FlowValueKind.Return, ref state);
        }

        /// <summary>
        /// Records the logical return value for an async state-machine completion path.
        /// </summary>
        /// <typeparam name="TReturn">The logical async return type.</typeparam>
        /// <param name="value">The logical return value.</param>
        /// <param name="state">The active recorder frame state.</param>
        public static void LogAsyncReturn<TReturn>(ref TReturn value, ref FlowRecorderState state)
        {
            try
            {
                LogValue(ref value, -1, FlowCapturePhase.Exit, FlowValueKind.Return, ref state);
            }
            catch
            {
                // Async return capture runs from SetResult and must never affect customer code.
            }
        }

        /// <summary>
        /// Records exception details for a recorder frame.
        /// </summary>
        /// <param name="state">The active recorder frame state.</param>
        /// <param name="exception">The exception to summarize.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordExceptionDetails(ref FlowRecorderState state, Exception exception)
        {
            if (!state.IsValid || exception is null)
            {
                return;
            }

            var session = Volatile.Read(ref _session);
            if (session?.Sink is null || state.Generation != session.Generation)
            {
                return;
            }

            session.Sink.TryEnqueueExceptionDetails(state.FlowId, state.FrameId, exception, session.Settings);
        }

        /// <summary>
        /// Writes buffered events to the configured output path or the provided path.
        /// </summary>
        /// <param name="path">Optional file path override.</param>
        /// <returns>The number of events written.</returns>
        public static int Flush(string? path = null)
        {
            lock (SyncRoot)
            {
                var session = GetOrCreateSession();
                if (session.Sink is null)
                {
                    return 0;
                }

                var file = session.Sink.DrainFile();
                var events = file.Events;
                var outputPath = path ?? session.Settings.OutputPath;
                var methods = session.Sink.GetMethodMetadata(events, outputPath);
                file = new FlowEventFile(events, methods, file.Strings, file.Types, file.Exceptions, file.Values, file.Operations);
                if (!StringUtil.IsNullOrEmpty(outputPath))
                {
                    try
                    {
                        FlowEventBinaryFormat.Write(outputPath, file);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Live debugger POC flow recorder failed to write events to {Path}", outputPath);
                    }
                }

                return events.Length;
            }
        }

        internal static FlowEvent[] DrainForTesting()
        {
            lock (SyncRoot)
            {
                return _session?.Sink?.Drain() ?? Array.Empty<FlowEvent>();
            }
        }

        internal static FlowEventFile DrainFileForTesting()
        {
            lock (SyncRoot)
            {
                var file = _session?.Sink?.DrainFile() ?? new FlowEventFile(Array.Empty<FlowEvent>(), Array.Empty<FlowMethodMetadata>());
                return new FlowEventFile(file.Events, _session?.Sink?.GetMethodMetadata(file.Events, metadataSidecarPath: null) ?? Array.Empty<FlowMethodMetadata>(), file.Strings, file.Types, file.Exceptions, file.Values, file.Operations);
            }
        }

        internal static void ConfigureForTesting(
            bool enabled,
            int bufferSize = FlowRecorderSettings.DefaultBufferSize,
            FlowValueCaptureMode valueCaptureMode = FlowValueCaptureMode.Off,
            string? valueCaptureMethodFilter = null,
            int valueBufferSize = FlowRecorderSettings.DefaultValueBufferSize,
            int maxStringLength = FlowRecorderSettings.DefaultMaxStringLength,
            int maxCollectionItems = FlowRecorderSettings.DefaultMaxCollectionItems,
            FlowValuePreviewMode valuePreviewMode = FlowValuePreviewMode.Off,
            int maxObjectFields = FlowRecorderSettings.DefaultMaxObjectFields,
            int maxChildValuesPerValue = FlowRecorderSettings.DefaultMaxChildValuesPerValue,
            int maxStackLength = FlowRecorderSettings.DefaultMaxStackLength,
            int maxEventsPerOperation = FlowRecorderSettings.DefaultMaxEventsPerOperation,
            int maxDepth = FlowRecorderSettings.DefaultMaxDepth,
            int maxDurationMs = FlowRecorderSettings.DefaultMaxDurationMs,
            int maxUniqueMethodsPerOperation = FlowRecorderSettings.DefaultMaxUniqueMethodsPerOperation,
            bool allowRecordingWithoutOperation = true)
        {
            lock (SyncRoot)
            {
                var settings = new FlowRecorderSettingsForTesting(enabled, bufferSize, valueCaptureMode, valueCaptureMethodFilter, valueBufferSize, maxStringLength, maxCollectionItems, valuePreviewMode, maxObjectFields, maxChildValuesPerValue, maxStackLength, maxEventsPerOperation, maxDepth, maxDurationMs, maxUniqueMethodsPerOperation, allowRecordingWithoutOperation);
                ClearThreadStaticState();
                var generation = ++_generation;
                _nextFlowId = 0;
                _nextFrameId = 0;
                _nextOperationId = 0;
                _droppedEvents = 0;
                Volatile.Write(ref _session, CreateSession(generation, settings, useWindowsGate: false));
                _initialized = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetEnabledSession(out RecorderSession session)
        {
            session = GetOrCreateSession();
            return session.Sink is not null;
        }

        private static RecorderSession GetOrCreateSession()
        {
            var session = Volatile.Read(ref _session);
            if (session is not null && _initialized)
            {
                return session;
            }

            lock (SyncRoot)
            {
                session = Volatile.Read(ref _session);
                if (session is not null && _initialized)
                {
                    return session;
                }

                var settings = FlowRecorderSettings.FromEnvironment();
                var generation = ++_generation;
                session = CreateSession(generation, settings, useWindowsGate: true);
                Volatile.Write(ref _session, session);
                _initialized = true;
                return session;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetActiveOperation(RecorderSession session, out FlowRecorderOperationContext? operationContext, out ulong operationId)
        {
            if (session.ActiveOperationCount <= 0 && !session.Settings.AllowRecordingWithoutOperation)
            {
                operationContext = null;
                operationId = 0;
                return false;
            }

            var operation = CurrentOperation.Value;
            if (operation is not null && operation.Generation == session.Generation && operation.IsActive)
            {
                operationContext = operation;
                operationId = operation.OperationId;
                _currentOperationId = operationId;
                _currentOperationGeneration = session.Generation;
                return true;
            }

            if (session.Settings.AllowRecordingWithoutOperation)
            {
                operationContext = null;
                operationId = 0;
                return true;
            }

            operationContext = null;
            operationId = 0;
            return false;
        }

        private static RecorderSession CreateSession(long generation, FlowRecorderSettings settings, bool useWindowsGate)
        {
            var enabled = settings.Enabled && (!useWindowsGate || FrameworkDescription.Instance.IsWindows());
            return new RecorderSession(generation, settings, enabled ? new FlowRecorderSink(settings.BufferSize, settings.ValueBufferSize) : null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong NextId(ref long counter)
        {
            return unchecked((ulong)Interlocked.Increment(ref counter));
        }

        private static void ClearThreadStaticState()
        {
            _currentAsyncOperationId = 0;
            _currentAsyncOperationGeneration = 0;
            _currentFlowGeneration = 0;
            _currentFlowId = 0;
            _currentFrameId = 0;
            _currentDepth = 0;
            _currentOperationId = 0;
            _currentOperationGeneration = 0;
            CurrentOperation.Value = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Enqueue(RecorderSession session, in FlowEvent flowEvent)
        {
            if (session.Settings.SkipEventEnqueue)
            {
                return;
            }

            if (session.Sink?.TryEnqueue(session, flowEvent) != true && ReferenceEquals(Volatile.Read(ref _session), session))
            {
                Interlocked.Increment(ref _droppedEvents);
            }
        }

        private static FlowEvent CreateEvent(FlowEventKind kind, int methodMetadataIndex, ulong operationId, ulong flowId, ulong frameId, ulong parentFrameId, int depth, long exceptionTypeId)
        {
            return new FlowEvent(
                kind,
                Stopwatch.GetTimestamp(),
                methodMetadataIndex,
                flowId,
                frameId,
                parentFrameId,
                depth,
                Environment.CurrentManagedThreadId,
                exceptionTypeId,
                operationId);
        }

        private static void LogValue<T>(ref T value, int index, FlowCapturePhase phase, FlowValueKind kind, ref FlowRecorderState state)
        {
            if (!ShouldCaptureValues(ref state, phase))
            {
                return;
            }

            var session = Volatile.Read(ref _session);
            if (session?.Sink is null || state.Generation != session.Generation)
            {
                return;
            }

            session.Sink.TryEnqueueValue(state.FlowId, state.FrameId, phase, kind, index, value, session.Settings);
        }

        private static void LogNamedValue<T>(ref T value, string name, FlowCapturePhase phase, FlowValueKind kind, ref FlowRecorderState state)
        {
            if (!ShouldCaptureValues(ref state, phase))
            {
                return;
            }

            var session = Volatile.Read(ref _session);
            if (session?.Sink is null || state.Generation != session.Generation)
            {
                return;
            }

            session.Sink.TryEnqueueValue(state.FlowId, state.FrameId, phase, kind, name, value, session.Settings);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldCapturePhase(FlowValueCaptureMode mode, FlowCapturePhase phase)
        {
            switch (mode)
            {
                case FlowValueCaptureMode.All:
                    return true;
                case FlowValueCaptureMode.Exceptions:
                    return phase == FlowCapturePhase.Exception;
                case FlowValueCaptureMode.Entry:
                    return phase == FlowCapturePhase.Entry;
                case FlowValueCaptureMode.Exit:
                    return phase == FlowCapturePhase.Exit || phase == FlowCapturePhase.Exception;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetCurrentAsyncOperationId(long generation)
        {
            if (_currentAsyncOperationGeneration == generation)
            {
                return _currentAsyncOperationId;
            }

            _currentAsyncOperationId = 0;
            _currentAsyncOperationGeneration = 0;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RestoreAsyncOperationId(in FlowRecorderState state)
        {
            if (_currentAsyncOperationGeneration == state.Generation && _currentAsyncOperationId == state.FlowId)
            {
                _currentAsyncOperationId = state.PreviousAsyncOperationId;
                _currentAsyncOperationGeneration = state.PreviousAsyncOperationGeneration;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RestoreFrameContext(ref FlowRecorderState state)
        {
            var isCurrentFrame = _currentFlowGeneration == state.Generation &&
                                 _currentFlowId == state.FlowId &&
                                 _currentFrameId == state.FrameId;
            if (isCurrentFrame)
            {
                var newDepth = state.Depth > 0 ? state.Depth - 1 : 0;
                if (newDepth == 0)
                {
                    _currentFlowGeneration = 0;
                    _currentFlowId = 0;
                    _currentFrameId = 0;
                    _currentDepth = 0;
                }
                else
                {
                    _currentFlowGeneration = state.Generation;
                    _currentFlowId = state.FlowId;
                    _currentFrameId = state.ParentFrameId;
                    _currentDepth = newDepth;
                }
            }

            if (state.RestoreAsyncOperationId)
            {
                RestoreAsyncOperationId(state);
            }
        }

        private static void GetTraceCorrelation(out ulong traceIdUpper, out ulong traceIdLower, out ulong rootSpanId, out ulong activeSpanId)
        {
            traceIdUpper = 0;
            traceIdLower = 0;
            rootSpanId = 0;
            activeSpanId = 0;

            var activeSpan = Tracer.Instance.InternalActiveScope?.Span;
            if (activeSpan is null)
            {
                return;
            }

            traceIdUpper = activeSpan.TraceId128.Upper;
            traceIdLower = activeSpan.TraceId128.Lower;
            rootSpanId = activeSpan.RootSpanId;
            activeSpanId = activeSpan.SpanId;
        }

        private static void ThrowIfFaultInjectionEnabled(bool enabled, string callbackName)
        {
            if (enabled)
            {
                throw new InvalidOperationException("Live debugger POC flow recorder forced " + callbackName + " failure.");
            }
        }

        private static string GetValueName(FlowValueKind kind, int index)
        {
            if (kind == FlowValueKind.Return)
            {
                return "@return";
            }

            if ((uint)index < CachedValueNameCount)
            {
                return kind == FlowValueKind.Argument ? ArgumentNames[index] : LocalNames[index];
            }

            return kind == FlowValueKind.Argument ? "arg" + index : "local" + index;
        }

        private static string[] CreateIndexedValueNames(string prefix)
        {
            var names = new string[CachedValueNameCount];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = prefix + i;
            }

            return names;
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (value is null || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private static bool TryGetCollectionCount(object value, out int count)
        {
            if (value is ICollection collection)
            {
                count = collection.Count;
                return true;
            }

            count = -1;
            return false;
        }

        private static bool TryConvertSigned<T>(T value, out long result)
        {
            switch (value)
            {
                case sbyte typed:
                    result = typed;
                    return true;
                case short typed:
                    result = typed;
                    return true;
                case int typed:
                    result = typed;
                    return true;
                case long typed:
                    result = typed;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        private static bool TryConvertUnsigned<T>(T value, out ulong result)
        {
            switch (value)
            {
                case byte typed:
                    result = typed;
                    return true;
                case ushort typed:
                    result = typed;
                    return true;
                case uint typed:
                    result = typed;
                    return true;
                case ulong typed:
                    result = typed;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        private sealed class RecorderSession
        {
            private long _activeOperationCount;

            public RecorderSession(long generation, FlowRecorderSettings settings, FlowRecorderSink? sink)
            {
                Generation = generation;
                Settings = settings;
                Sink = sink;
            }

            public long Generation { get; }

            public FlowRecorderSettings Settings { get; }

            public FlowRecorderSink? Sink { get; }

            public long ActiveOperationCount => Volatile.Read(ref _activeOperationCount);

            public void IncrementActiveOperationCount()
            {
                Interlocked.Increment(ref _activeOperationCount);
            }

            public void DecrementActiveOperationCount()
            {
                while (true)
                {
                    var current = Volatile.Read(ref _activeOperationCount);
                    if (current <= 0)
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(ref _activeOperationCount, current - 1, current) == current)
                    {
                        return;
                    }
                }
            }
        }

        private sealed class FlowRecorderOperationScope : IDisposable
        {
            public static readonly IDisposable Noop = new NoopDisposable();

            private readonly RecorderSession _session;
            private readonly FlowRecorderOperationContext _operation;
            private readonly ulong _previousOperationId;
            private readonly long _previousOperationGeneration;
            private readonly FlowRecorderOperationContext? _previousOperation;

            public FlowRecorderOperationScope(RecorderSession session, FlowRecorderOperationContext operation, ulong previousOperationId, long previousOperationGeneration, FlowRecorderOperationContext? previousOperation)
            {
                _session = session;
                _operation = operation;
                _previousOperationId = previousOperationId;
                _previousOperationGeneration = previousOperationGeneration;
                _previousOperation = previousOperation;
            }

            public void Dispose()
            {
                if (_operation.TryDeactivate())
                {
                    _session.DecrementActiveOperationCount();
                }

                if (ReferenceEquals(CurrentOperation.Value, _operation))
                {
                    CurrentOperation.Value = _previousOperation;
                }

                if (_currentOperationGeneration == _operation.Generation && _currentOperationId == _operation.OperationId)
                {
                    _currentOperationId = _previousOperationId;
                    _currentOperationGeneration = _previousOperationGeneration;
                }
            }

            private sealed class NoopDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }

        private sealed class FlushOnDisposeScope : IDisposable
        {
            private readonly IDisposable _inner;
            private readonly string _outputPath;
            private bool _disposed;

            public FlushOnDisposeScope(IDisposable inner, string outputPath)
            {
                _inner = inner;
                _outputPath = outputPath;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _inner.Dispose();
                Flush(_outputPath);
            }
        }

        private sealed class FlowRecorderSink
        {
            private const int MethodRegistrationStateCount = 16_384;
            private const int MaxPreviewTypeCount = 4_096;
            private const int MaxPreviewNameCount = 32_768;

            private static readonly MethodInfo TryEnqueueTypedChildValueMethod = typeof(FlowRecorderSink).GetMethod(nameof(TryEnqueueTypedChildValue), BindingFlags.Instance | BindingFlags.NonPublic)!;

            private readonly BoundedRecorderBuffer<FlowEvent> _queue;
            private readonly BoundedRecorderBuffer<FlowExceptionDetails> _exceptionQueue;
            private readonly BoundedRecorderBuffer<FlowCapturedValue> _valueQueue;
            private readonly int[] _methodRegistrationStates;
            private readonly object _metadataLock = new();
            private readonly object _stringLock = new();
            private readonly object _previewTypeLock = new();
            private readonly Dictionary<int, FlowMethodMetadata> _methods = new();
            private readonly Dictionary<ulong, FlowOperationMetadata> _operations = new();
            private readonly Dictionary<ulong, OperationBudgetState> _operationBudgets = new();
            private readonly Dictionary<Type, FieldPreviewDescriptor[]> _previewTypes = new();
            private readonly Dictionary<FieldPreviewNameKey, CollectionItemNameCache> _previewFieldNames = new();
            private readonly Dictionary<CollectionItemNameKey, CollectionItemNameCache> _previewItemNames = new();
            private Dictionary<string, int> _strings = new();
            private List<string> _stringTable = new();
            private Dictionary<string, int> _types = new();
            private List<string> _typeTable = new();
            private int _stringTableGeneration;
            private int _activeDrainFileWriters;
            private int _drainingFile;

            public FlowRecorderSink(int bufferSize, int valueBufferSize)
            {
                _queue = new BoundedRecorderBuffer<FlowEvent>(bufferSize);
                _exceptionQueue = new BoundedRecorderBuffer<FlowExceptionDetails>(valueBufferSize);
                _valueQueue = new BoundedRecorderBuffer<FlowCapturedValue>(valueBufferSize);
                _methodRegistrationStates = new int[MethodRegistrationStateCount];
            }

            private delegate bool FieldPreviewWriter(FlowRecorderSink sink, object target, ulong flowId, ulong frameId, FlowCapturePhase phase, FlowValueKind kind, int nameId, FlowRecorderSettings settings, ref int remaining);

            public bool TryEnqueue(RecorderSession session, in FlowEvent flowEvent)
            {
                if (!TryEnterDrainFileWriter())
                {
                    return false;
                }

                try
                {
                    if (TryApplyBudget(session, flowEvent, out var marker))
                    {
                        return _queue.TryEnqueue(flowEvent);
                    }

                    return marker.Kind != default && _queue.TryEnqueue(marker);
                }
                finally
                {
                    ExitDrainFileWriter();
                }
            }

            public bool TryEnqueueExceptionDetails(ulong flowId, ulong frameId, Exception exception, FlowRecorderSettings settings)
            {
                if (!TryEnterDrainFileWriter())
                {
                    return false;
                }

                try
                {
                    var typeId = GetTypeId(exception.GetType());
                    var messageId = GetStringId(Truncate(exception.Message, settings.MaxStringLength));
                    var stackId = GetStringId(Truncate(exception.StackTrace, settings.MaxStackLength));
                    return _exceptionQueue.TryEnqueue(new FlowExceptionDetails(flowId, frameId, typeId, messageId, stackId, exception.HResult));
                }
                finally
                {
                    ExitDrainFileWriter();
                }
            }

            public bool TryEnqueueValue<T>(ulong flowId, ulong frameId, FlowCapturePhase phase, FlowValueKind kind, int index, T value, FlowRecorderSettings settings)
            {
                if (!TryEnterDrainFileWriter())
                {
                    return false;
                }

                try
                {
                    var type = typeof(T);
                    var name = GetValueName(kind, index);
                    var nameId = GetStringId(name);
                    var typeId = GetTypeId(type);
                    var captured = CreateCapturedValue(flowId, frameId, phase, kind, nameId, typeId, value, type, settings);
                    if (!_valueQueue.TryEnqueue(captured))
                    {
                        return false;
                    }

                    if (settings.ValuePreviewMode == FlowValuePreviewMode.Shallow)
                    {
                        TryEnqueueValuePreview(flowId, frameId, phase, kind, name, value, settings);
                    }

                    return true;
                }
                finally
                {
                    ExitDrainFileWriter();
                }
            }

            public bool TryEnqueueValue<T>(ulong flowId, ulong frameId, FlowCapturePhase phase, FlowValueKind kind, string name, T value, FlowRecorderSettings settings)
            {
                if (!TryEnterDrainFileWriter())
                {
                    return false;
                }

                try
                {
                    var type = typeof(T);
                    var valueName = StringUtil.IsNullOrEmpty(name) ? GetValueName(kind, -1) : name;
                    var nameId = GetStringId(valueName);
                    var typeId = GetTypeId(type);
                    var captured = CreateCapturedValue(flowId, frameId, phase, kind, nameId, typeId, value, type, settings);
                    if (!_valueQueue.TryEnqueue(captured))
                    {
                        return false;
                    }

                    if (settings.ValuePreviewMode == FlowValuePreviewMode.Shallow)
                    {
                        TryEnqueueValuePreview(flowId, frameId, phase, kind, valueName, value, settings);
                    }

                    return true;
                }
                finally
                {
                    ExitDrainFileWriter();
                }
            }

            public void TryRegisterOperation(FlowOperationMetadata operation)
            {
                if (operation.OperationId == 0)
                {
                    return;
                }

                lock (_metadataLock)
                {
                    if (!_operations.ContainsKey(operation.OperationId))
                    {
                        _operations[operation.OperationId] = operation;
                    }
                }
            }

            private bool TryApplyBudget(RecorderSession session, in FlowEvent flowEvent, out FlowEvent marker)
            {
                marker = default;
                if (flowEvent.OperationId == 0 || flowEvent.Kind is FlowEventKind.Truncated or FlowEventKind.Suppressed)
                {
                    return true;
                }

                lock (_metadataLock)
                {
                    if (!_operationBudgets.TryGetValue(flowEvent.OperationId, out var budget))
                    {
                        budget = new OperationBudgetState(flowEvent.Timestamp);
                        _operationBudgets[flowEvent.OperationId] = budget;
                    }

                    if (budget.Truncated)
                    {
                        return false;
                    }

                    if (flowEvent.Depth > session.Settings.MaxDepth)
                    {
                        budget.Truncated = true;
                        marker = CreateBudgetMarker(flowEvent, FlowEventKind.Truncated, "depth limit");
                        return false;
                    }

                    if (budget.EventCount >= session.Settings.MaxEventsPerOperation)
                    {
                        budget.Truncated = true;
                        marker = CreateBudgetMarker(flowEvent, FlowEventKind.Truncated, "event limit");
                        return false;
                    }

                    var elapsedMs = (flowEvent.Timestamp - budget.StartTimestamp) * 1000 / Stopwatch.Frequency;
                    if (elapsedMs > session.Settings.MaxDurationMs)
                    {
                        budget.Truncated = true;
                        marker = CreateBudgetMarker(flowEvent, FlowEventKind.Truncated, "duration limit");
                        return false;
                    }

                    if (!budget.ContainsMethod(flowEvent.MethodMetadataIndex) && budget.MethodCount >= session.Settings.MaxUniqueMethodsPerOperation)
                    {
                        if (budget.SuppressedMethodLimit)
                        {
                            return false;
                        }

                        budget.SuppressedMethodLimit = true;
                        marker = CreateBudgetMarker(flowEvent, FlowEventKind.Suppressed, "unique method limit");
                        return false;
                    }

                    budget.AddMethod(flowEvent.MethodMetadataIndex);
                    budget.EventCount++;
                    return true;
                }
            }

            private FlowEvent CreateBudgetMarker(in FlowEvent source, FlowEventKind kind, string reason)
            {
                return new FlowEvent(
                    kind,
                    source.Timestamp,
                    methodMetadataIndex: GetStringId(reason),
                    source.FlowId,
                    source.FrameId,
                    source.ParentFrameId,
                    source.Depth,
                    source.ThreadId,
                    exceptionTypeId: 0,
                    source.OperationId);
            }

            public void TryRegisterMethod(int methodMetadataIndex, RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle)
            {
                if (methodMetadataIndex < 0)
                {
                    return;
                }

                var hasRegistrationState = (uint)methodMetadataIndex < (uint)_methodRegistrationStates.Length;
                if (hasRegistrationState && Volatile.Read(ref _methodRegistrationStates[methodMetadataIndex]) != 0)
                {
                    return;
                }

                MethodBase? method;
                Type? type;
                try
                {
                    method = MethodBase.GetMethodFromHandle(methodHandle, typeHandle);
                    type = Type.GetTypeFromHandle(typeHandle);
                }
                catch (Exception ex)
                {
                    Log.Debug<int>(ex, "Live debugger POC flow recorder failed to resolve method metadata for index {MethodMetadataIndex}", methodMetadataIndex);
                    return;
                }

                if (method is null || type is null)
                {
                    return;
                }

                var metadata = new FlowMethodMetadata(methodMetadataIndex, FormatMethodName(type, method));
                lock (_metadataLock)
                {
                    if (!_methods.ContainsKey(methodMetadataIndex))
                    {
                        _methods[methodMetadataIndex] = metadata;
                    }
                }

                MarkRegistered(methodMetadataIndex, hasRegistrationState);
            }

            public void TryRegisterMethodName(int methodMetadataIndex, string displayName)
            {
                if (methodMetadataIndex < 0 || StringUtil.IsNullOrEmpty(displayName))
                {
                    return;
                }

                var metadata = new FlowMethodMetadata(methodMetadataIndex, displayName);
                lock (_metadataLock)
                {
                    if (!_methods.ContainsKey(methodMetadataIndex))
                    {
                        _methods[methodMetadataIndex] = metadata;
                    }
                }

                MarkRegistered(methodMetadataIndex, (uint)methodMetadataIndex < (uint)_methodRegistrationStates.Length);
            }

            public FlowEvent[] Drain()
            {
                return _queue.Drain();
            }

            public FlowEventFile DrainFile()
            {
                EnterDrainFile();
                try
                {
                    var events = Drain();
                    var exceptions = DrainExceptions();
                    var values = DrainValues();
                    IReadOnlyList<string> strings;
                    IReadOnlyList<string> types;
                    lock (_stringLock)
                    {
                        strings = _stringTable;
                        types = _typeTable;
                        _strings = new Dictionary<string, int>();
                        _stringTable = new List<string>();
                        _types = new Dictionary<string, int>();
                        _typeTable = new List<string>();
                        unchecked
                        {
                            _stringTableGeneration++;
                        }
                    }

                    FlowOperationMetadata[] operations;
                    lock (_metadataLock)
                    {
                        operations = new FlowOperationMetadata[_operations.Count];
                        _operations.Values.CopyTo(operations, 0);
                    }

                    Array.Sort(operations, static (left, right) => left.OperationId.CompareTo(right.OperationId));

                    return new FlowEventFile(events, Array.Empty<FlowMethodMetadata>(), strings, types, exceptions, values, operations);
                }
                finally
                {
                    Volatile.Write(ref _drainingFile, 0);
                }
            }

            public FlowMethodMetadata[] GetMethodMetadata(FlowEvent[] events, string? metadataSidecarPath)
            {
                if (events.Length == 0)
                {
                    return Array.Empty<FlowMethodMetadata>();
                }

                var usedMethodIds = new int[events.Length];
                for (var i = 0; i < events.Length; i++)
                {
                    usedMethodIds[i] = events[i].MethodMetadataIndex;
                }

                Array.Sort(usedMethodIds);
                TryRegisterMethodsFromSidecar(metadataSidecarPath);

                var methods = new FlowMethodMetadata[usedMethodIds.Length];
                var methodCount = 0;
                var previousMethodId = int.MinValue;
                var hasPreviousMethodId = false;
                lock (_metadataLock)
                {
                    foreach (var methodId in usedMethodIds)
                    {
                        if (hasPreviousMethodId && methodId == previousMethodId)
                        {
                            continue;
                        }

                        hasPreviousMethodId = true;
                        previousMethodId = methodId;
                        if (_methods.TryGetValue(methodId, out var method))
                        {
                            methods[methodCount] = method;
                            methodCount++;
                        }
                    }
                }

                if (methodCount == 0)
                {
                    return Array.Empty<FlowMethodMetadata>();
                }

                if (methodCount != methods.Length)
                {
                    Array.Resize(ref methods, methodCount);
                }

                return methods;
            }

            private static string UnescapeSidecarField(string value)
            {
                if (value.IndexOf('\\') < 0)
                {
                    return value;
                }

                var result = new StringBuilder(value.Length);
                for (var i = 0; i < value.Length; i++)
                {
                    var ch = value[i];
                    if (ch != '\\' || i == value.Length - 1)
                    {
                        result.Append(ch);
                        continue;
                    }

                    var escaped = value[++i];
                    switch (escaped)
                    {
                        case 't':
                            result.Append('\t');
                            break;
                        case 'r':
                            result.Append('\r');
                            break;
                        case 'n':
                            result.Append('\n');
                            break;
                        case '\\':
                            result.Append('\\');
                            break;
                        default:
                            result.Append(escaped);
                            break;
                    }
                }

                return result.ToString();
            }

            private static string FormatMethodName(Type type, MethodBase method)
            {
                var declaringType = method.DeclaringType ?? type;
                return declaringType.FullName + "." + method.Name;
            }

            private static MethodInfo GetTypedChildValueWriter(Type fieldType)
            {
                return TryEnqueueTypedChildValueMethod.MakeGenericMethod(fieldType);
            }

            private void TryRegisterMethodsFromSidecar(string? capturePath)
            {
                if (StringUtil.IsNullOrEmpty(capturePath))
                {
                    return;
                }

                var sidecarPath = capturePath + ".methods";
                if (!File.Exists(sidecarPath))
                {
                    return;
                }

                try
                {
                    foreach (var line in File.ReadLines(sidecarPath))
                    {
                        if (StringUtil.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        var separatorIndex = line.IndexOf('\t');
                        if (separatorIndex <= 0)
                        {
                            continue;
                        }

                        var methodMetadataIndexText = line.Substring(0, separatorIndex);
#pragma warning disable CA1846 // Span-based int parsing is unavailable on older target frameworks.
                        if (!int.TryParse(methodMetadataIndexText, out var methodMetadataIndex))
#pragma warning restore CA1846
                        {
                            continue;
                        }

                        var displayName = UnescapeSidecarField(line.Substring(separatorIndex + 1));
                        TryRegisterMethodName(methodMetadataIndex, displayName);
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Live debugger POC flow recorder failed to read method metadata sidecar {Path}", sidecarPath);
                }
            }

            private void MarkRegistered(int methodMetadataIndex, bool hasRegistrationState)
            {
                if (hasRegistrationState)
                {
                    Volatile.Write(ref _methodRegistrationStates[methodMetadataIndex], 1);
                }
            }

            private FlowExceptionDetails[] DrainExceptions()
            {
                return _exceptionQueue.Drain();
            }

            private FlowCapturedValue[] DrainValues()
            {
                return _valueQueue.Drain();
            }

            private FlowCapturedValue CreateCapturedValue<T>(
                ulong flowId,
                ulong frameId,
                FlowCapturePhase phase,
                FlowValueKind kind,
                int nameId,
                int typeId,
                T value,
                Type type,
                FlowRecorderSettings settings)
            {
                // Zero-allocation fast paths for common non-nullable value types.
                // For value types, typeof(T) == typeof(X) folds to a JIT-time constant, so the
                // untaken branches are eliminated and Unsafe.As reinterprets without boxing.
                // Nullable value types, boxed values, and reference types fall through to the
                // pattern-matching body below (which is the only path that may box).
                if (typeof(T) == typeof(bool))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.Boolean, FlowNotCapturedReason.None, Unsafe.As<T, bool>(ref value) ? 1 : 0, -1, -1, -1);
                }

                if (typeof(T) == typeof(int))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.Int64, FlowNotCapturedReason.None, Unsafe.As<T, int>(ref value), -1, -1, -1);
                }

                if (typeof(T) == typeof(long))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.Int64, FlowNotCapturedReason.None, Unsafe.As<T, long>(ref value), -1, -1, -1);
                }

                if (typeof(T) == typeof(short))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.Int64, FlowNotCapturedReason.None, Unsafe.As<T, short>(ref value), -1, -1, -1);
                }

                if (typeof(T) == typeof(sbyte))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.Int64, FlowNotCapturedReason.None, Unsafe.As<T, sbyte>(ref value), -1, -1, -1);
                }

                if (typeof(T) == typeof(byte))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.UInt64, FlowNotCapturedReason.None, Unsafe.As<T, byte>(ref value), -1, -1, -1);
                }

                if (typeof(T) == typeof(ushort))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.UInt64, FlowNotCapturedReason.None, Unsafe.As<T, ushort>(ref value), -1, -1, -1);
                }

                if (typeof(T) == typeof(uint))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.UInt64, FlowNotCapturedReason.None, Unsafe.As<T, uint>(ref value), -1, -1, -1);
                }

                if (typeof(T) == typeof(ulong))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.UInt64, FlowNotCapturedReason.None, unchecked((long)Unsafe.As<T, ulong>(ref value)), -1, -1, -1);
                }

                if (typeof(T) == typeof(char))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.UInt64, FlowNotCapturedReason.None, Unsafe.As<T, char>(ref value), -1, -1, -1);
                }

                if (typeof(T) == typeof(float))
                {
                    return CreateStringValue(flowId, frameId, phase, kind, nameId, typeId, Unsafe.As<T, float>(ref value).ToString(), settings);
                }

                if (typeof(T) == typeof(double))
                {
                    return CreateStringValue(flowId, frameId, phase, kind, nameId, typeId, Unsafe.As<T, double>(ref value).ToString(), settings);
                }

                if (typeof(T) == typeof(decimal))
                {
                    return CreateStringValue(flowId, frameId, phase, kind, nameId, typeId, Unsafe.As<T, decimal>(ref value).ToString(), settings);
                }

                if (value is null)
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.Null, FlowNotCapturedReason.None, 0, -1, -1, -1);
                }

                if (value is bool boolValue)
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.Boolean, FlowNotCapturedReason.None, boolValue ? 1 : 0, -1, -1, -1);
                }

                if (value is char charValue)
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.UInt64, FlowNotCapturedReason.None, charValue, -1, -1, -1);
                }

                if (TryConvertSigned(value, out var signedValue))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.Int64, FlowNotCapturedReason.None, signedValue, -1, -1, -1);
                }

                if (TryConvertUnsigned(value, out var unsignedValue))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.UInt64, FlowNotCapturedReason.None, unchecked((long)unsignedValue), -1, -1, -1);
                }

                if (value is float floatValue)
                {
                    return CreateStringValue(flowId, frameId, phase, kind, nameId, typeId, floatValue.ToString(), settings);
                }

                if (value is double doubleValue)
                {
                    return CreateStringValue(flowId, frameId, phase, kind, nameId, typeId, doubleValue.ToString(), settings);
                }

                if (value is decimal decimalValue)
                {
                    return CreateStringValue(flowId, frameId, phase, kind, nameId, typeId, decimalValue.ToString(), settings);
                }

                if (value is string stringValue)
                {
                    return CreateStringValue(flowId, frameId, phase, kind, nameId, typeId, stringValue, settings);
                }

                if (value is IEnumerable && TryGetCollectionCount(value, out var count))
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.CollectionSummary, count > settings.MaxCollectionItems ? FlowNotCapturedReason.CollectionSize : FlowNotCapturedReason.None, 0, -1, count, Math.Min(count, settings.MaxCollectionItems));
                }

                if (value is IEnumerable)
                {
                    return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.CollectionSummary, FlowNotCapturedReason.CollectionSize, 0, -1, -1, settings.MaxCollectionItems);
                }

                return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.TypeSummary, FlowNotCapturedReason.None, 0, GetStringId(type.Name), -1, -1);
            }

            private FlowCapturedValue CreateStringValue(
                ulong flowId,
                ulong frameId,
                FlowCapturePhase phase,
                FlowValueKind kind,
                int nameId,
                int typeId,
                string? value,
                FlowRecorderSettings settings)
            {
                var truncated = Truncate(value, settings.MaxStringLength);
                var reason = value is not null && value.Length > settings.MaxStringLength ? FlowNotCapturedReason.PayloadLimit : FlowNotCapturedReason.None;
                return new FlowCapturedValue(flowId, frameId, phase, kind, nameId, typeId, FlowValueTag.String, reason, 0, GetStringId(truncated), -1, -1);
            }

            private void TryEnqueueValuePreview<T>(
                ulong flowId,
                ulong frameId,
                FlowCapturePhase phase,
                FlowValueKind kind,
                string rootName,
                T value,
                FlowRecorderSettings settings)
            {
                if (settings.MaxChildValuesPerValue <= 0)
                {
                    return;
                }

                if (value is null || value is string)
                {
                    return;
                }

                try
                {
                    var remaining = settings.MaxChildValuesPerValue;
                    if (TryEnqueueCollectionPreview(flowId, frameId, phase, kind, rootName, value, settings, ref remaining))
                    {
                        return;
                    }

                    if (!ShouldPreviewObjectFields(typeof(T), value))
                    {
                        return;
                    }

                    TryEnqueueObjectFieldPreview(flowId, frameId, phase, kind, rootName, value!, settings, ref remaining);
                }
                catch
                {
                    // Value preview must never affect customer code. The root value was already captured.
                }
            }

            private bool TryEnqueueCollectionPreview<T>(
                ulong flowId,
                ulong frameId,
                FlowCapturePhase phase,
                FlowValueKind kind,
                string rootName,
                T value,
                FlowRecorderSettings settings,
                ref int remaining)
            {
                if (remaining <= 0 || value is null)
                {
                    return false;
                }

                if (value is Array array)
                {
                    if (array.Rank != 1)
                    {
                        return true;
                    }

                    if (value is object[] objectArray)
                    {
                        TryEnqueueArrayPreview(flowId, frameId, phase, kind, rootName, objectArray, settings, ref remaining);
                        return true;
                    }

                    if (value is string[] stringArray)
                    {
                        TryEnqueueArrayPreview(flowId, frameId, phase, kind, rootName, stringArray, settings, ref remaining);
                        return true;
                    }

                    var count = Math.Min(array.Length, settings.MaxCollectionItems);
                    for (var i = 0; i < count && remaining > 0; i++)
                    {
                        var item = array.GetValue(i);
                        var itemName = GetCollectionItemName(rootName, i);
                        if (!TryEnqueueChildValue(flowId, frameId, phase, kind, itemName.NameId, item, item?.GetType() ?? typeof(object), settings, ref remaining))
                        {
                            return true;
                        }

                        if (item is not null && remaining > 0 && ShouldPreviewObjectFields(item.GetType(), item))
                        {
                            TryEnqueueObjectFieldPreview(flowId, frameId, phase, kind, itemName.Name, item, settings, ref remaining);
                        }
                    }

                    return true;
                }

                if (value is IList list)
                {
                    var count = Math.Min(list.Count, settings.MaxCollectionItems);
                    for (var i = 0; i < count && remaining > 0; i++)
                    {
                        object? item;
                        try
                        {
                            item = list[i];
                        }
                        catch
                        {
                            return true;
                        }

                        var itemName = GetCollectionItemName(rootName, i);
                        if (!TryEnqueueChildValue(flowId, frameId, phase, kind, itemName.NameId, item, item?.GetType() ?? typeof(object), settings, ref remaining))
                        {
                            return true;
                        }

                        if (item is not null && remaining > 0 && ShouldPreviewObjectFields(item.GetType(), item))
                        {
                            TryEnqueueObjectFieldPreview(flowId, frameId, phase, kind, itemName.Name, item, settings, ref remaining);
                        }
                    }

                    return true;
                }

                return value is IEnumerable;
            }

            private void TryEnqueueArrayPreview<TItem>(
                ulong flowId,
                ulong frameId,
                FlowCapturePhase phase,
                FlowValueKind kind,
                string rootName,
                TItem[] array,
                FlowRecorderSettings settings,
                ref int remaining)
            {
                var count = Math.Min(array.Length, settings.MaxCollectionItems);
                for (var i = 0; i < count && remaining > 0; i++)
                {
                    var item = array[i];
                    var itemName = GetCollectionItemName(rootName, i);
                    var itemType = item?.GetType() ?? typeof(TItem);
                    if (!TryEnqueueChildValue(flowId, frameId, phase, kind, itemName.NameId, item, itemType, settings, ref remaining))
                    {
                        return;
                    }

                    if (item is not null && remaining > 0 && ShouldPreviewObjectFields(itemType, item))
                    {
                        TryEnqueueObjectFieldPreview(flowId, frameId, phase, kind, itemName.Name, item, settings, ref remaining);
                    }
                }
            }

            private void TryEnqueueObjectFieldPreview(
                ulong flowId,
                ulong frameId,
                FlowCapturePhase phase,
                FlowValueKind kind,
                string rootName,
                object value,
                FlowRecorderSettings settings,
                ref int remaining)
            {
                if (remaining <= 0 || settings.MaxObjectFields <= 0)
                {
                    return;
                }

                var descriptors = GetFieldPreviewDescriptors(value.GetType());
                var count = Math.Min(descriptors.Length, settings.MaxObjectFields);
                for (var i = 0; i < count && remaining > 0; i++)
                {
                    var descriptor = descriptors[i];
                    var fieldName = GetFieldPreviewName(rootName, descriptors, i);
                    if (!descriptor.Writer(this, value, flowId, frameId, phase, kind, fieldName.NameId, settings, ref remaining))
                    {
                        return;
                    }
                }
            }

            private bool TryEnqueueTypedChildValue<TField>(
                ulong flowId,
                ulong frameId,
                FlowCapturePhase phase,
                FlowValueKind kind,
                int nameId,
                TField value,
                FlowRecorderSettings settings,
                ref int remaining)
            {
                if (remaining <= 0)
                {
                    return false;
                }

                var type = typeof(TField);
                var typeId = GetTypeId(type);
                var captured = CreateCapturedValue(flowId, frameId, phase, kind, nameId, typeId, value, type, settings);
                if (!_valueQueue.TryEnqueue(captured))
                {
                    remaining = 0;
                    return false;
                }

                remaining--;
                return true;
            }

            private bool TryEnqueueChildValue(
                ulong flowId,
                ulong frameId,
                FlowCapturePhase phase,
                FlowValueKind kind,
                int nameId,
                object? value,
                Type type,
                FlowRecorderSettings settings,
                ref int remaining)
            {
                if (remaining <= 0)
                {
                    return false;
                }

                var typeId = GetTypeId(type);
                var captured = CreateCapturedValue<object?>(flowId, frameId, phase, kind, nameId, typeId, value, type, settings);
                if (!_valueQueue.TryEnqueue(captured))
                {
                    remaining = 0;
                    return false;
                }

                remaining--;
                return true;
            }

            private FieldPreviewDescriptor[] CreateFieldPreviewDescriptors(Type type)
            {
                FieldInfo[] fields;
                try
                {
                    fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                catch
                {
                    return Array.Empty<FieldPreviewDescriptor>();
                }

                if (fields.Length == 0)
                {
                    return Array.Empty<FieldPreviewDescriptor>();
                }

                var descriptors = new FieldPreviewDescriptor[fields.Length];
                var count = 0;
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (field.IsStatic || ShouldSkipField(field))
                    {
                        continue;
                    }

                    var writer = TryCreateFieldPreviewWriter(field);
                    if (writer is null)
                    {
                        continue;
                    }

                    descriptors[count++] = new FieldPreviewDescriptor(NormalizeFieldName(field.Name), writer);
                }

                if (count != descriptors.Length)
                {
                    Array.Resize(ref descriptors, count);
                }

                return descriptors;
            }

            private FieldPreviewWriter? TryCreateFieldPreviewWriter(FieldInfo field)
            {
                try
                {
                    var declaringType = field.DeclaringType;
                    if (declaringType is null || declaringType.ContainsGenericParameters || field.FieldType.ContainsGenericParameters)
                    {
                        return null;
                    }

                    var dynamicMethod = new DynamicMethod(
                        "DatadogFlowRecorderFieldPreview",
                        typeof(bool),
                        new[] { typeof(FlowRecorderSink), typeof(object), typeof(ulong), typeof(ulong), typeof(FlowCapturePhase), typeof(FlowValueKind), typeof(int), typeof(FlowRecorderSettings), typeof(int).MakeByRefType() },
                        typeof(FlowRecorderSink),
                        skipVisibility: true);

                    var il = dynamicMethod.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_3);
                    il.Emit(OpCodes.Ldarg_S, 4);
                    il.Emit(OpCodes.Ldarg_S, 5);
                    il.Emit(OpCodes.Ldarg_S, 6);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Castclass, declaringType);
                    il.Emit(OpCodes.Ldfld, field);
                    il.Emit(OpCodes.Ldarg_S, 7);
                    il.Emit(OpCodes.Ldarg_S, 8);
                    il.Emit(OpCodes.Call, GetTypedChildValueWriter(field.FieldType));
                    il.Emit(OpCodes.Ret);
                    return (FieldPreviewWriter)dynamicMethod.CreateDelegate(typeof(FieldPreviewWriter));
                }
                catch
                {
                    return null;
                }
            }

            private CollectionItemNameCache GetFieldPreviewName(string rootName, FieldPreviewDescriptor[] descriptors, int index)
            {
                var key = new FieldPreviewNameKey(rootName, descriptors, index);
                lock (_previewTypeLock)
                {
                    if (_previewFieldNames.TryGetValue(key, out var cached) && cached.Generation == _stringTableGeneration)
                    {
                        return cached;
                    }

                    var name = rootName + "." + descriptors[index].Name;
                    cached = new CollectionItemNameCache(_stringTableGeneration, name, GetStringId(name));
                    if (_previewFieldNames.Count < MaxPreviewNameCount)
                    {
                        _previewFieldNames[key] = cached;
                    }

                    return cached;
                }
            }

            private FieldPreviewDescriptor[] GetFieldPreviewDescriptors(Type type)
            {
                FieldPreviewDescriptor[]? descriptors;
                lock (_previewTypeLock)
                {
                    if (!_previewTypes.TryGetValue(type, out descriptors))
                    {
                        descriptors = CreateFieldPreviewDescriptors(type);
                        if (_previewTypes.Count < MaxPreviewTypeCount)
                        {
                            _previewTypes[type] = descriptors;
                        }
                    }
                }

                return descriptors;
            }

            private bool ShouldSkipField(FieldInfo field)
            {
                var fieldType = field.FieldType;
                if (fieldType.IsPointer || fieldType.IsByRef)
                {
                    return true;
                }

                var name = field.Name;
                return name.Length > 0 && name[0] == '<' && !IsAutoPropertyBackingField(name);
            }

            private bool ShouldPreviewObjectFields<T>(Type staticType, T value)
            {
                if (value is null)
                {
                    return false;
                }

                var type = value.GetType();
                return !type.IsPrimitive &&
                       !type.IsEnum &&
                       !type.IsValueType &&
                       !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) &&
                       type != typeof(string) &&
                       type != typeof(decimal) &&
                       type != typeof(DateTime) &&
                       type != typeof(DateTimeOffset) &&
                       type != typeof(TimeSpan) &&
                       !(value is IEnumerable);
            }

            private string NormalizeFieldName(string name)
            {
                if (IsAutoPropertyBackingField(name))
                {
                    var endIndex = name.IndexOf('>');
                    if (endIndex > 1)
                    {
                        return name.Substring(1, endIndex - 1);
                    }
                }

                return name;
            }

            private bool IsAutoPropertyBackingField(string name)
            {
                return name.StartsWith("<", StringComparison.Ordinal) &&
                       name.EndsWith(">k__BackingField", StringComparison.Ordinal);
            }

            private CollectionItemNameCache GetCollectionItemName(string rootName, int index)
            {
                var key = new CollectionItemNameKey(rootName, index);
                lock (_previewTypeLock)
                {
                    if (_previewItemNames.TryGetValue(key, out var cached) && cached.Generation == _stringTableGeneration)
                    {
                        return cached;
                    }

                    var name = rootName + "[" + index + "]";
                    cached = new CollectionItemNameCache(_stringTableGeneration, name, GetStringId(name));
                    if (_previewItemNames.Count < MaxPreviewNameCount)
                    {
                        _previewItemNames[key] = cached;
                    }

                    return cached;
                }
            }

            private int GetStringId(string? value)
            {
                if (StringUtil.IsNullOrEmpty(value))
                {
                    return -1;
                }

                lock (_stringLock)
                {
                    if (_strings.TryGetValue(value, out var id))
                    {
                        return id;
                    }

                    id = _stringTable.Count;
                    _strings[value] = id;
                    _stringTable.Add(value);
                    return id;
                }
            }

            private int GetTypeId(Type type)
            {
                var name = type.FullName ?? type.Name;
                lock (_stringLock)
                {
                    if (_types.TryGetValue(name, out var id))
                    {
                        return id;
                    }

                    id = _typeTable.Count;
                    _types[name] = id;
                    _typeTable.Add(name);
                    return id;
                }
            }

            private bool TryEnterDrainFileWriter()
            {
                while (true)
                {
                    if (Volatile.Read(ref _drainingFile) != 0)
                    {
                        return false;
                    }

                    Interlocked.Increment(ref _activeDrainFileWriters);
                    if (Volatile.Read(ref _drainingFile) == 0)
                    {
                        return true;
                    }

                    Interlocked.Decrement(ref _activeDrainFileWriters);
                }
            }

            private void ExitDrainFileWriter()
            {
                Interlocked.Decrement(ref _activeDrainFileWriters);
            }

            private void EnterDrainFile()
            {
                while (Interlocked.CompareExchange(ref _drainingFile, 1, 0) != 0)
                {
                    Thread.Yield();
                }

                var spinner = default(SpinWait);
                while (Volatile.Read(ref _activeDrainFileWriters) != 0)
                {
                    spinner.SpinOnce();
                }
            }

            private readonly struct FieldPreviewDescriptor
            {
                public FieldPreviewDescriptor(string name, FieldPreviewWriter writer)
                {
                    Name = name;
                    Writer = writer;
                }

                public string Name { get; }

                public FieldPreviewWriter Writer { get; }
            }

            private readonly struct CollectionItemNameCache
            {
                public CollectionItemNameCache(int generation, string name, int nameId)
                {
                    Generation = generation;
                    Name = name;
                    NameId = nameId;
                }

                public int Generation { get; }

                public string Name { get; }

                public int NameId { get; }
            }

            private readonly struct FieldPreviewNameKey : IEquatable<FieldPreviewNameKey>
            {
                private readonly string _rootName;
                private readonly FieldPreviewDescriptor[] _descriptors;
                private readonly int _index;

                public FieldPreviewNameKey(string rootName, FieldPreviewDescriptor[] descriptors, int index)
                {
                    _rootName = rootName;
                    _descriptors = descriptors;
                    _index = index;
                }

                public bool Equals(FieldPreviewNameKey other)
                {
                    return _index == other._index &&
                           ReferenceEquals(_descriptors, other._descriptors) &&
                           string.Equals(_rootName, other._rootName, StringComparison.Ordinal);
                }

                public override bool Equals(object? obj)
                {
                    return obj is FieldPreviewNameKey other && Equals(other);
                }

                public override int GetHashCode()
                {
                    return (((RuntimeHelpers.GetHashCode(_descriptors) * 397) ^ _index) * 397) ^ StringComparer.Ordinal.GetHashCode(_rootName);
                }
            }

            private readonly struct CollectionItemNameKey : IEquatable<CollectionItemNameKey>
            {
                private readonly string _rootName;
                private readonly int _index;

                public CollectionItemNameKey(string rootName, int index)
                {
                    _rootName = rootName;
                    _index = index;
                }

                public bool Equals(CollectionItemNameKey other)
                {
                    return _index == other._index &&
                           string.Equals(_rootName, other._rootName, StringComparison.Ordinal);
                }

                public override bool Equals(object? obj)
                {
                    return obj is CollectionItemNameKey other && Equals(other);
                }

                public override int GetHashCode()
                {
                    return (_index * 397) ^ StringComparer.Ordinal.GetHashCode(_rootName);
                }
            }

            private sealed class OperationBudgetState
            {
                private const int InlineMethodCount = 8;

                private int _method0;
                private int _method1;
                private int _method2;
                private int _method3;
                private int _method4;
                private int _method5;
                private int _method6;
                private int _method7;
                private HashSet<int>? _overflowMethods;

                public OperationBudgetState(long startTimestamp)
                {
                    StartTimestamp = startTimestamp;
                }

                public long StartTimestamp { get; }

                public int EventCount { get; set; }

                public bool Truncated { get; set; }

                public bool SuppressedMethodLimit { get; set; }

                public int MethodCount { get; private set; }

                public bool ContainsMethod(int methodMetadataIndex)
                {
                    var inlineCount = Math.Min(MethodCount, InlineMethodCount);
                    for (var i = 0; i < inlineCount; i++)
                    {
                        if (GetInlineMethod(i) == methodMetadataIndex)
                        {
                            return true;
                        }
                    }

                    return _overflowMethods?.Contains(methodMetadataIndex) == true;
                }

                public void AddMethod(int methodMetadataIndex)
                {
                    if (ContainsMethod(methodMetadataIndex))
                    {
                        return;
                    }

                    if (MethodCount < InlineMethodCount)
                    {
                        SetInlineMethod(MethodCount, methodMetadataIndex);
                    }
                    else
                    {
                        _overflowMethods ??= new HashSet<int>();
                        _overflowMethods.Add(methodMetadataIndex);
                    }

                    MethodCount++;
                }

                private int GetInlineMethod(int index)
                {
                    switch (index)
                    {
                        case 0:
                            return _method0;
                        case 1:
                            return _method1;
                        case 2:
                            return _method2;
                        case 3:
                            return _method3;
                        case 4:
                            return _method4;
                        case 5:
                            return _method5;
                        case 6:
                            return _method6;
                        default:
                            return _method7;
                    }
                }

                private void SetInlineMethod(int index, int methodMetadataIndex)
                {
                    switch (index)
                    {
                        case 0:
                            _method0 = methodMetadataIndex;
                            break;
                        case 1:
                            _method1 = methodMetadataIndex;
                            break;
                        case 2:
                            _method2 = methodMetadataIndex;
                            break;
                        case 3:
                            _method3 = methodMetadataIndex;
                            break;
                        case 4:
                            _method4 = methodMetadataIndex;
                            break;
                        case 5:
                            _method5 = methodMetadataIndex;
                            break;
                        case 6:
                            _method6 = methodMetadataIndex;
                            break;
                        default:
                            _method7 = methodMetadataIndex;
                            break;
                    }
                }
            }
        }

        private sealed class BoundedRecorderBuffer<T>
            where T : struct
        {
            private T[] _items;
            private int[] _published;
            private int _writeIndex;
            private int _activeWriters;
            private int _draining;

            public BoundedRecorderBuffer(int capacity)
            {
                if (capacity <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(capacity), "Buffer size must be positive.");
                }

                _items = new T[capacity];
                _published = new int[capacity];
            }

            public bool TryEnqueue(in T item)
            {
                if (!TryEnterWriter())
                {
                    return false;
                }

                try
                {
                    if (!TryReserveSlot(out var index))
                    {
                        return false;
                    }

                    _items[index] = item;
                    Volatile.Write(ref _published[index], 1);
                    return true;
                }
                finally
                {
                    Interlocked.Decrement(ref _activeWriters);
                }
            }

            public T[] Drain()
            {
                while (Interlocked.CompareExchange(ref _draining, 1, 0) != 0)
                {
                    Thread.Yield();
                }

                try
                {
                    var spinner = default(SpinWait);
                    while (Volatile.Read(ref _activeWriters) != 0)
                    {
                        spinner.SpinOnce();
                    }

                    var count = Math.Min(Volatile.Read(ref _writeIndex), _items.Length);
                    if (count <= 0)
                    {
                        return Array.Empty<T>();
                    }

                    for (var i = 0; i < count; i++)
                    {
                        while (Volatile.Read(ref _published[i]) == 0)
                        {
                            spinner.SpinOnce();
                        }
                    }

                    T[] result;
                    if (count == _items.Length)
                    {
                        result = _items;
                        _items = new T[result.Length];
                        _published = new int[result.Length];
                    }
                    else
                    {
                        result = new T[count];
                        Array.Copy(_items, result, count);
                        Array.Clear(_items, 0, count);
                        Array.Clear(_published, 0, count);
                    }

                    Volatile.Write(ref _writeIndex, 0);
                    return result;
                }
                finally
                {
                    Volatile.Write(ref _draining, 0);
                }
            }

            private bool TryEnterWriter()
            {
                while (true)
                {
                    if (Volatile.Read(ref _draining) != 0)
                    {
                        return false;
                    }

                    Interlocked.Increment(ref _activeWriters);
                    if (Volatile.Read(ref _draining) == 0)
                    {
                        return true;
                    }

                    Interlocked.Decrement(ref _activeWriters);
                }
            }

            private bool TryReserveSlot(out int index)
            {
                while (true)
                {
                    var current = Volatile.Read(ref _writeIndex);
                    if ((uint)current >= (uint)_items.Length)
                    {
                        index = -1;
                        return false;
                    }

                    if (Interlocked.CompareExchange(ref _writeIndex, current + 1, current) == current)
                    {
                        index = current;
                        return true;
                    }
                }
            }
        }

        private sealed class FlowRecorderSettingsForTesting : FlowRecorderSettings
        {
            public FlowRecorderSettingsForTesting(
                bool enabled,
                int bufferSize,
                FlowValueCaptureMode valueCaptureMode,
                string? valueCaptureMethodFilter,
                int valueBufferSize,
                int maxStringLength,
                int maxCollectionItems,
                FlowValuePreviewMode valuePreviewMode,
                int maxObjectFields,
                int maxChildValuesPerValue,
                int maxStackLength,
                int maxEventsPerOperation,
                int maxDepth,
                int maxDurationMs,
                int maxUniqueMethodsPerOperation,
                bool allowRecordingWithoutOperation)
                : base(
                    enabled,
                    outputPath: null,
                    bufferSize,
                    triggerReason: null,
                    root: null,
                    valueCaptureMode,
                    valueCaptureMethodFilter,
                    valueBufferSize,
                    maxStringLength,
                    maxCollectionItems,
                    valuePreviewMode,
                    maxObjectFields,
                    maxChildValuesPerValue,
                    maxStackLength,
                    maxEventsPerOperation,
                    maxDepth,
                    maxDurationMs,
                    maxUniqueMethodsPerOperation,
                    allowRecordingWithoutOperation: allowRecordingWithoutOperation)
            {
            }
        }
    }
}
