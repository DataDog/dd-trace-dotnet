// <copyright file="ExceptionDebuggingProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using Fnv1aHash = Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal.Hash;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionDebuggingProcessor : IProbeProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExceptionDebuggingProcessor));
        private readonly object _lock = new();
        private readonly int _maxFramesToCapture;
        private ExceptionProbeProcessor[] _processors;

        internal ExceptionDebuggingProcessor(string probeId, MethodUniqueIdentifier method)
        {
            _processors = Array.Empty<ExceptionProbeProcessor>();
            ProbeId = probeId;
            Method = method;
            _maxFramesToCapture = ExceptionDebugging.Settings.MaximumFramesToCapture;
        }

        public string ProbeId { get; }

        public MethodUniqueIdentifier Method { get; }

        public bool ShouldProcess(in ProbeData probeData)
        {
            if (!ShadowStackHolder.IsShadowStackTrackingEnabled)
            {
                // In Exception Debugging V1, we only care about exceptions propagating up the call stack in webservice apps, when there's a request in-flight.
                // When we will support Caught & Logged Exceptions, we will need to adjust this.
                // `IsShadowStackTrackingEnabled` is equivalent to using `ShadowStackTree.IsInRequestContext`, as of now.
                return false;
            }

            var shadowStack = ShadowStackHolder.EnsureShadowStackEnabled();

            if (shadowStack.CurrentStackFrameNode?.IsInvalidPath == true)
            {
                return false;
            }

            return true;
        }

        public bool Process<TCapture>(ref CaptureInfo<TCapture> info, IDebuggerSnapshotCreator inSnapshotCreator, in ProbeData probeData)
        {
            var snapshotCreator = (ExceptionSnapshotCreator)inSnapshotCreator;
            ShadowStackTree shadowStack;

            try
            {
                switch (info.MethodState)
                {
                    case MethodState.BeginLine:
                    case MethodState.BeginLineAsync:
                        break;
                    case MethodState.EntryStart:
                    case MethodState.EntryAsync:
                        shadowStack = ShadowStackHolder.EnsureShadowStackEnabled();
                        snapshotCreator.EnterHash = shadowStack.CurrentStackFrameNode?.EnterSequenceHash ?? Fnv1aHash.FnvOffsetBias;

                        var shouldProcess = false;
                        foreach (var processor in snapshotCreator.Processors)
                        {
                            if (processor.ShouldProcess(snapshotCreator.EnterHash))
                            {
                                // TODO consider if the processors that successfully entered, should be used solely on Leave without any noise.
                                // TODO     this requirement arises due to Invalidation that could happen to EnterHash if a new method pops in the call path.
                                shouldProcess = true;
                            }
                        }

                        var enteredNode = shadowStack.Enter(info.Method, isInvalidPath: !shouldProcess);
                        snapshotCreator.TrackedStackFrameNode = enteredNode;
                        return true;
                    case MethodState.ExitStart:
                    case MethodState.ExitStartAsync:
                        if (snapshotCreator.TrackedStackFrameNode == null)
                        {
                            throw new InvalidOperationException("Encountered invalid state of snapshotCreator in ExitState/ExitStateAsync. `snapshotCreator.TrackedStackFrameNode` should not be null.");
                        }

                        if (snapshotCreator.TrackedStackFrameNode.IsFrameUnwound)
                        {
                            Log.Warning("ExceptionDebuggingProcessor: Frame is already unwound. Probe Id: {ProbeId}", ProbeId);
                            return false;
                        }

                        shadowStack = ShadowStackHolder.EnsureShadowStackEnabled();

                        if (snapshotCreator.TrackedStackFrameNode.IsInvalidPath)
                        {
                            shadowStack.Leave(snapshotCreator.TrackedStackFrameNode);
                            return false;
                        }

                        if (info.MemberKind != ScopeMemberKind.Exception || info.Value == null)
                        {
                            shadowStack.Leave(snapshotCreator.TrackedStackFrameNode);
                            return false;
                        }

                        var exception = info.Value as Exception;
                        snapshotCreator.TrackedStackFrameNode.LeavingException = exception;
                        snapshotCreator.LeaveHash = shadowStack.CurrentStackFrameNode?.LeaveSequenceHash ?? Fnv1aHash.FnvOffsetBias;

                        var leavingExceptionType = info.Value.GetType();

                        foreach (var processor in snapshotCreator.Processors)
                        {
                            if (processor.Leave(leavingExceptionType, snapshotCreator))
                            {
                                var isRoot = shadowStack.Leave(snapshotCreator.TrackedStackFrameNode, exception);

                                if (!isRoot && snapshotCreator.TrackedStackFrameNode.NumOfChildren > _maxFramesToCapture)
                                {
                                    // Capture no more.
                                    snapshotCreator.TrackedStackFrameNode.CapturingStrategy = SnapshotCapturingStrategy.None;
                                    return true;
                                }

                                // Full Snapshot / Lightweight

                                var sequenceHash = snapshotCreator.TrackedStackFrameNode.SequenceHash;
                                if (!shadowStack.ContainsUniqueId(sequenceHash))
                                {
                                    shadowStack.AddUniqueId(sequenceHash);
                                    snapshotCreator.TrackedStackFrameNode.CapturingStrategy = SnapshotCapturingStrategy.FullSnapshot;
                                }
                                else
                                {
                                    snapshotCreator.TrackedStackFrameNode.CapturingStrategy = SnapshotCapturingStrategy.LightweightSnapshot;
                                }

                                snapshotCreator.TrackedStackFrameNode.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);

                                return true;
                            }
                        }

                        shadowStack.Leave(snapshotCreator.TrackedStackFrameNode);
                        return false;
                    case MethodState.EntryEnd:
                    case MethodState.EndLine:
                    case MethodState.EndLineAsync:
                        break;
                    case MethodState.ExitEnd:
                    case MethodState.ExitEndAsync:
                        if (snapshotCreator.TrackedStackFrameNode == null)
                        {
                            throw new InvalidOperationException("Encountered invalid state of snapshotCreator in ExitEnd/ExitEndAsync. `snapshotCreator.TrackedStackFrameNode` should not be null.");
                        }

                        if (snapshotCreator.TrackedStackFrameNode.CapturingStrategy != SnapshotCapturingStrategy.None)
                        {
                            snapshotCreator.TrackedStackFrameNode.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);

                            snapshotCreator.TrackedStackFrameNode.MethodMetadataIndex = info.MethodMetadataIndex;
                            snapshotCreator.TrackedStackFrameNode.ProbeId = ProbeId;
                            snapshotCreator.TrackedStackFrameNode.HasArgumentsOrLocals = info.HasLocalOrArgument;

                            if (info.IsAsyncCapture())
                            {
                                snapshotCreator.TrackedStackFrameNode.IsAsyncMethod = true;
                                snapshotCreator.TrackedStackFrameNode.MoveNextInvocationTarget = info.AsyncCaptureInfo.MoveNextInvocationTarget;
                                snapshotCreator.TrackedStackFrameNode.KickoffInvocationTarget = info.AsyncCaptureInfo.KickoffInvocationTarget;
                            }

                            if (snapshotCreator.TrackedStackFrameNode.CapturingStrategy == SnapshotCapturingStrategy.FullSnapshot)
                            {
                                _ = snapshotCreator.TrackedStackFrameNode.Snapshot;
                            }
                        }

                        return true;
                    case MethodState.LogLocal:
                    case MethodState.LogArg:
                        if (snapshotCreator.TrackedStackFrameNode == null)
                        {
                            throw new InvalidOperationException("Encountered invalid state of snapshotCreator in LogLocal/LogArg. `snapshotCreator.TrackedStackFrameNode` should not be null.");
                        }

                        if (snapshotCreator.TrackedStackFrameNode.CapturingStrategy != SnapshotCapturingStrategy.None)
                        {
                            snapshotCreator.TrackedStackFrameNode.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                        }

                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "ExceptionDebuggingProcessor: Failed to process probe. Probe Id: {ProbeId}", ProbeId);
                return false;
            }

            return true;
        }

        public void LogException(Exception ex, IDebuggerSnapshotCreator inSnapshotCreator)
        {
            var snapshotCreator = (ExceptionSnapshotCreator)inSnapshotCreator;

            if (snapshotCreator.TrackedStackFrameNode == null)
            {
                return;
            }

            var shadowStack = ShadowStackHolder.EnsureShadowStackEnabled();
            shadowStack.Leave(snapshotCreator.TrackedStackFrameNode);
        }

        public IProbeProcessor UpdateProbeProcessor(ProbeDefinition probe)
        {
            return this;
        }

        public IDebuggerSnapshotCreator CreateSnapshotCreator()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            return new ExceptionSnapshotCreator(_processors, ProbeId);
        }

        public void AddProbeProcessor(ExceptionProbeProcessor processor)
        {
            lock (_lock)
            {
                var newProcessors = new ExceptionProbeProcessor[_processors.Length + 1];
                Array.Copy(_processors, newProcessors, _processors.Length);
                newProcessors[newProcessors.Length - 1] = processor;
                _processors = newProcessors;
            }
        }

        public int RemoveProbeProcessor(ExceptionProbeProcessor processor)
        {
            lock (_lock)
            {
                var index = Array.IndexOf(_processors, processor);
                if (index < 0)
                {
                    return -1;
                }

                if (_processors.Length == 1)
                {
                    _processors = Array.Empty<ExceptionProbeProcessor>();
                    return 0;
                }

                var newProcessors = new ExceptionProbeProcessor[_processors.Length - 1];

                if (index > 0)
                {
                    Array.Copy(_processors, 0, newProcessors, 0, index);
                }

                if (index < _processors.Length - 1)
                {
                    Array.Copy(_processors, index + 1, newProcessors, index, _processors.Length - index - 1);
                }

                _processors = newProcessors;

                return _processors.Length;
            }
        }

        public void InvalidateEnterLeave()
        {
            lock (_lock)
            {
                foreach (var processor in _processors)
                {
                    processor.InvalidateEnterLeave();
                }
            }
        }
    }
}
