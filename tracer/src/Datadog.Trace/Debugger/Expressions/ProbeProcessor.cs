// <copyright file="ProbeProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Expressions
{
    internal class ProbeProcessor
    {
        protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeProcessor));

        private ProbeExpressionEvaluator _evaluator;

        public ProbeProcessor(ProbeDefinition probe)
        {
            var location = probe.Where.MethodName != null
                               ? ProbeLocation.Method
                               : ProbeLocation.Line;

            var probeType = probe switch
            {
                LogProbe { Capture: { } } => ProbeType.Snapshot,
                MetricProbe => ProbeType.Metric,
                _ => ProbeType.Log
            };

            ProbeInfo = new ProbeInfo(
                probe.Id,
                probeType,
                location,
                probe.EvaluateAt,
                Templates: (probe as LogProbe)?.Segments?.Select(s => Convert(s).Value).ToArray(),
                Condition: Convert((probe as LogProbe)?.When),
                Metric: Convert((probe as MetricProbe)?.Value));
        }

        internal ProbeInfo ProbeInfo { get; }

        private DebuggerExpression? Convert(SnapshotSegment segment)
        {
            return segment == null ? null : new DebuggerExpression(segment.Dsl, segment.Json?.ToString(), segment.Str);
        }

        private ProbeExpressionEvaluator GetOrCreateEvaluator(MethodScopeMembers scopeMembers)
        {
            Interlocked.CompareExchange(ref _evaluator, new ProbeExpressionEvaluator(ProbeInfo.Templates, ProbeInfo.Condition, ProbeInfo.Metric, scopeMembers), null);
            return _evaluator;
        }

        internal bool Process<TCapture>(ref CaptureInfo<TCapture> info, DebuggerSnapshotCreator snapshotCreator)
        {
            ExpressionEvaluationResult evaluationResult = default;
            try
            {
                switch (info.MethodState)
                {
                    case MethodState.BeginLine:
                    case MethodState.BeginLineAsync:
                    case MethodState.EntryStart:
                    case MethodState.EntryAsync:
                        var captureBehaviour = snapshotCreator.DefineSnapshotBehavior(info, ProbeInfo.EvaluateAt, HasCondition());
                        if (captureBehaviour != CaptureBehaviour.Capture)
                        {
                            return true;
                        }

                        break;
                    case MethodState.ExitStart:
                    case MethodState.ExitStartAsync:
                        captureBehaviour = snapshotCreator.DefineSnapshotBehavior(info, ProbeInfo.EvaluateAt, HasCondition());
                        switch (captureBehaviour)
                        {
                            case CaptureBehaviour.Stop:
                                return true;
                            case CaptureBehaviour.Delay:
                                snapshotCreator.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                                return true;
                            case CaptureBehaviour.Capture:
                                snapshotCreator.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(
                                    nameof(captureBehaviour),
                                    $"{captureBehaviour} is not valid value here");
                        }

                        break;
                    case MethodState.EntryEnd:
                    case MethodState.EndLine:
                    case MethodState.EndLineAsync:
                    case MethodState.ExitEnd:
                    case MethodState.ExitEndAsync:
                        {
                            captureBehaviour = snapshotCreator.DefineSnapshotBehavior(info, ProbeInfo.EvaluateAt, HasCondition());
                            switch (captureBehaviour)
                            {
                                case CaptureBehaviour.NoCapture or CaptureBehaviour.Stop:
                                    return true;
                                case CaptureBehaviour.Capture:
                                    snapshotCreator.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                                    break;
                                case CaptureBehaviour.Evaluate:
                                    {
                                        snapshotCreator.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                                        try
                                        {
                                            evaluationResult = GetOrCreateEvaluator(snapshotCreator.MethodScopeMembers).Evaluate();
                                        }
                                        catch (Exception e)
                                        {
                                            // if the evaluation failed stop capturing
                                            Log.Error(e, "Failed to evaluate expression for probe: " + ProbeInfo.ProbeId);
                                            snapshotCreator.CaptureBehaviour = CaptureBehaviour.NoCapture;
                                            return true;
                                        }

                                        if (evaluationResult.Condition is false && (evaluationResult.Errors == null || evaluationResult.Errors.Count == 0))
                                        {
                                            // if the expression evaluated to false
                                            snapshotCreator.Stop();
                                            return true;
                                        }

                                        if (evaluationResult.Metric.HasValue)
                                        {
                                            LiveDebugger.Instance.SendMetrics();
                                        }

                                        break;
                                    }

                                default:
                                    throw new ArgumentOutOfRangeException(
                                        nameof(captureBehaviour),
                                        $"{captureBehaviour} is not valid value here");
                            }

                            break;
                        }

                    case MethodState.LogLocal:
                    case MethodState.LogArg:
                    case MethodState.LogException:
                        if (snapshotCreator.CaptureBehaviour is CaptureBehaviour.NoCapture or CaptureBehaviour.Stop)
                        {
                            // there is a condition that should evaluate at exit phase and we are at entry phase
                            return true;
                        }

                        snapshotCreator.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                        if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Delay)
                        {
                            return true;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                ProcessCapture(ref info, ref snapshotCreator, ref evaluationResult);
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to process probe. Probe Id: " + ProbeInfo.ProbeId);
                return false;
            }
        }

        private void ProcessCapture<TCapture>(ref CaptureInfo<TCapture> info, ref DebuggerSnapshotCreator snapshotCreator, ref ExpressionEvaluationResult evaluationResult)
        {
            // if no condition or condition has evaluates to true, check limit
            if (!ProbeRateLimiter.Instance.Sample(ProbeInfo.ProbeId))
            {
                return;
            }

            switch (ProbeInfo.ProbeLocation)
            {
                case ProbeLocation.Method:
                    ProcessMethod(ref info, ref snapshotCreator, ref evaluationResult);
                    break;
                case ProbeLocation.Line:
                    ProcessLine(ref info, ref snapshotCreator, ref evaluationResult);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ProcessMethod<TCapture>(ref CaptureInfo<TCapture> info, ref DebuggerSnapshotCreator snapshotCreator, ref ExpressionEvaluationResult evaluationResult)
        {
            switch (info.MethodState)
            {
                case MethodState.EntryStart:
                    snapshotCreator.CaptureEntryMethodStartMarker(ref info);
                    break;
                case MethodState.EntryAsync:
                    snapshotCreator.CaptureEntryAsyncMethod(ref info);
                    break;
                case MethodState.EntryEnd:
                    if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Evaluate)
                    {
                        snapshotCreator.SetEvaluationResult(ref evaluationResult);
                    }

                    if (!ProbeInfo.IsFullSnapshot)
                    {
                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(ProbeInfo.ProbeId, ref info);
                        LiveDebugger.Instance.AddSnapshot(snapshot);
                        break;
                    }

                    snapshotCreator.ProcessQueue(ref info, HasCondition());
                    snapshotCreator.CaptureEntryMethodEndMarker(info.Value, info.Type, info.HasLocalOrArgument.Value);

                    break;
                case MethodState.ExitStart:
                case MethodState.ExitStartAsync:
                    snapshotCreator.CaptureExitMethodStartMarker(ref info);
                    break;
                case MethodState.ExitEnd:
                case MethodState.ExitEndAsync:
                    {
                        string snapshot = null;
                        if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Evaluate)
                        {
                            snapshotCreator.SetEvaluationResult(ref evaluationResult);
                        }

                        if (!ProbeInfo.IsFullSnapshot)
                        {
                            snapshot = snapshotCreator.FinalizeMethodSnapshot(ProbeInfo.ProbeId, ref info);
                            LiveDebugger.Instance.AddSnapshot(snapshot);
                            snapshotCreator.CaptureBehaviour = CaptureBehaviour.NoCapture;
                            break;
                        }

                        snapshotCreator.ProcessQueue(ref info, HasCondition());
                        snapshotCreator.CaptureExitMethodEndMarker(ref info);

                        snapshot = snapshotCreator.FinalizeMethodSnapshot(ProbeInfo.ProbeId, ref info);
                        LiveDebugger.Instance.AddSnapshot(snapshot);
                        break;
                    }

                case MethodState.LogArg:
                    snapshotCreator.CaptureArgument(info.Value, info.Name, info.Type);
                    break;
                case MethodState.LogLocal:
                    snapshotCreator.CaptureLocal(info.Value, info.Name, info.Type);
                    break;
                case MethodState.LogException:
                    snapshotCreator.CaptureException(info.Value as Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(info.MethodState));
            }
        }

        private void ProcessLine<TCapture>(ref CaptureInfo<TCapture> info, ref DebuggerSnapshotCreator snapshotCreator, ref ExpressionEvaluationResult evaluationResult)
        {
            switch (info.MethodState)
            {
                case MethodState.BeginLine:
                case MethodState.BeginLineAsync:
                    snapshotCreator.CaptureBeginLine(ref info);
                    break;

                case MethodState.EndLine:
                case MethodState.EndLineAsync:
                    snapshotCreator.ProcessQueue(ref info, HasCondition());
                    snapshotCreator.CaptureEndLine(ref info);
                    if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Evaluate)
                    {
                        snapshotCreator.SetEvaluationResult(ref evaluationResult);
                    }

                    var snapshot = snapshotCreator.FinalizeLineSnapshot(ProbeInfo.ProbeId, ref info);
                    LiveDebugger.Instance.AddSnapshot(snapshot);
                    break;

                case MethodState.LogArg:
                    snapshotCreator.CaptureArgument(info.Value, info.Name, info.Type);
                    break;
                case MethodState.LogLocal:
                    snapshotCreator.CaptureLocal(info.Value, info.Name, info.Type);
                    break;
                case MethodState.LogException:
                    snapshotCreator.CaptureException(info.Value as Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(info.MethodState));
            }
        }

        [DebuggerStepThrough]
        private bool HasCondition()
        {
            return ProbeInfo.Condition.HasValue;
        }
    }
}
