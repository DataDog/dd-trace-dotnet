// <copyright file="FlowRecorder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FlowRecorder));
        private static readonly AsyncLocal<FlowContext?> CurrentFlow = new();
        private static readonly object SyncRoot = new();

        private static RecorderSession? _session;
        private static long _generation;
        private static long _nextFlowId;
        private static long _nextFrameId;
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
                CurrentFlow.Value = null;
                var generation = ++_generation;
                Volatile.Write(ref _session, CreateSession(generation, settings, useWindowsGate: true));
                _nextFlowId = 0;
                _nextFrameId = 0;
                _droppedEvents = 0;
                _initialized = true;
            }
        }

        /// <summary>
        /// Starts a recorded method frame.
        /// </summary>
        /// <param name="methodMetadataIndex">The method metadata index assigned by native instrumentation.</param>
        /// <returns>Opaque state that must be passed to the exit callback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FlowRecorderState Enter(int methodMetadataIndex)
        {
            if (!TryGetEnabledSession(out var session))
            {
                return default;
            }

            var context = CurrentFlow.Value;
            if (context is null || context.Generation != session.Generation)
            {
                context = new FlowContext(session.Generation, NextId(ref _nextFlowId), currentFrameId: 0, depth: 0);
            }

            var parentFrameId = context.CurrentFrameId;
            var frameId = NextId(ref _nextFrameId);
            var depth = context.Depth + 1;
            CurrentFlow.Value = new FlowContext(session.Generation, context.FlowId, frameId, depth);

            var state = new FlowRecorderState(session.Generation, context.FlowId, frameId, parentFrameId, depth, methodMetadataIndex);
            Enqueue(session, CreateEvent(FlowEventKind.Enter, methodMetadataIndex, context.FlowId, frameId, parentFrameId, depth, exceptionTypeId: 0));
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

            var context = CurrentFlow.Value;
            var isCurrentFrame = context is not null &&
                                 context.Generation == state.Generation &&
                                 context.FlowId == state.FlowId &&
                                 context.CurrentFrameId == state.FrameId;
            if (isCurrentFrame)
            {
                var newDepth = state.Depth > 0 ? state.Depth - 1 : 0;
                CurrentFlow.Value = newDepth == 0
                                        ? null
                                        : new FlowContext(state.Generation, state.FlowId, state.ParentFrameId, newDepth);
            }

            var session = Volatile.Read(ref _session);
            if (session?.Sink is null || state.Generation != session.Generation)
            {
                return;
            }

            if (exception is not null)
            {
                Enqueue(session, CreateEvent(FlowEventKind.Exception, state.MethodMetadataIndex, state.FlowId, state.FrameId, state.ParentFrameId, state.Depth, RuntimeHelpers.GetHashCode(exception.GetType())));
            }

            Enqueue(session, CreateEvent(FlowEventKind.Exit, state.MethodMetadataIndex, state.FlowId, state.FrameId, state.ParentFrameId, state.Depth, exceptionTypeId: 0));
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

                var events = session.Sink.Drain();
                var outputPath = path ?? session.Settings.OutputPath;
                if (!StringUtil.IsNullOrEmpty(outputPath))
                {
                    try
                    {
                        FlowEventBinaryFormat.Write(outputPath, events);
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

        internal static void ConfigureForTesting(bool enabled, int bufferSize = FlowRecorderSettings.DefaultBufferSize)
        {
            lock (SyncRoot)
            {
                var settings = new FlowRecorderSettingsForTesting(enabled, bufferSize);
                CurrentFlow.Value = null;
                var generation = ++_generation;
                Volatile.Write(ref _session, CreateSession(generation, settings, useWindowsGate: false));
                _nextFlowId = 0;
                _nextFrameId = 0;
                _droppedEvents = 0;
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

        private static RecorderSession CreateSession(long generation, FlowRecorderSettings settings, bool useWindowsGate)
        {
            var enabled = settings.Enabled && (!useWindowsGate || FrameworkDescription.Instance.IsWindows());
            return new RecorderSession(generation, settings, enabled ? new FlowRecorderSink(settings.BufferSize) : null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong NextId(ref long counter)
        {
            return unchecked((ulong)Interlocked.Increment(ref counter));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Enqueue(RecorderSession session, in FlowEvent flowEvent)
        {
            if (session.Sink?.TryEnqueue(flowEvent) != true)
            {
                Interlocked.Increment(ref _droppedEvents);
            }
        }

        private static FlowEvent CreateEvent(FlowEventKind kind, int methodMetadataIndex, ulong flowId, ulong frameId, ulong parentFrameId, int depth, long exceptionTypeId)
        {
            GetTraceCorrelation(out var traceIdUpper, out var traceIdLower, out var rootSpanId, out var activeSpanId);

            return new FlowEvent(
                kind,
                Stopwatch.GetTimestamp(),
                methodMetadataIndex,
                flowId,
                frameId,
                parentFrameId,
                depth,
                Environment.CurrentManagedThreadId,
                traceIdUpper,
                traceIdLower,
                rootSpanId,
                activeSpanId,
                exceptionTypeId);
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

        private sealed class FlowContext
        {
            public FlowContext(long generation, ulong flowId, ulong currentFrameId, int depth)
            {
                Generation = generation;
                FlowId = flowId;
                CurrentFrameId = currentFrameId;
                Depth = depth;
            }

            public long Generation { get; }

            public ulong FlowId { get; }

            public ulong CurrentFrameId { get; }

            public int Depth { get; }
        }

        private sealed class RecorderSession
        {
            public RecorderSession(long generation, FlowRecorderSettings settings, FlowRecorderSink? sink)
            {
                Generation = generation;
                Settings = settings;
                Sink = sink;
            }

            public long Generation { get; }

            public FlowRecorderSettings Settings { get; }

            public FlowRecorderSink? Sink { get; }
        }

        private sealed class FlowRecorderSink
        {
            private readonly BoundedConcurrentQueue<FlowEvent> _queue;

            public FlowRecorderSink(int bufferSize)
            {
                _queue = new BoundedConcurrentQueue<FlowEvent>(bufferSize);
            }

            public bool TryEnqueue(FlowEvent flowEvent)
            {
                return _queue.TryEnqueue(flowEvent);
            }

            public FlowEvent[] Drain()
            {
                var events = new FlowEvent[_queue.Count];
                var index = 0;
                while (_queue.TryDequeue(out var flowEvent))
                {
                    if (index == events.Length)
                    {
                        Array.Resize(ref events, events.Length + 1);
                    }

                    events[index++] = flowEvent;
                }

                if (index != events.Length)
                {
                    Array.Resize(ref events, index);
                }

                return events;
            }
        }

        private sealed class FlowRecorderSettingsForTesting : FlowRecorderSettings
        {
            public FlowRecorderSettingsForTesting(bool enabled, int bufferSize)
                : base(enabled, outputPath: null, bufferSize)
            {
            }
        }
    }
}
