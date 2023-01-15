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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeProcessor));

        private ProbeExpressionEvaluator _evaluator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbeProcessor"/> class, that correlated to probe id
        /// </summary>
        /// <param name="probe">A probe that can pe log probe or metric probe</param>
        /// <exception cref="ArgumentOutOfRangeException">If probe type or probe location is from unsupported type</exception>
        /// <remarks>Exceptions should be caught and logged by the caller</remarks>
        internal ProbeProcessor(ProbeDefinition probe)
        {
            _evaluator = default;
            var location = probe.Where.MethodName != null
                               ? ProbeLocation.Method
                               : ProbeLocation.Line;

            var probeType = probe switch
            {
                LogProbe { CaptureSnapshot: true } => ProbeType.Snapshot,
                LogProbe { CaptureSnapshot: false } => ProbeType.Log,
                MetricProbe => ProbeType.Metric,
                _ => throw new ArgumentOutOfRangeException(nameof(probe), probe, "Unsupported probe type")
            };

            var evaluateAt = location switch
            {
                ProbeLocation.Method => probe.EvaluateAt,
                ProbeLocation.Line => EvaluateAt.Entry,
                _ => throw new ArgumentOutOfRangeException(nameof(location), location, "Unsupported probe location")
            };

            ProbeInfo = new ProbeInfo(
                probe.Id,
                probeType,
                location,
                evaluateAt,
                // ReSharper disable once PossibleInvalidOperationException
                Templates: (probe as LogProbe)?.Segments?.Where(seg => seg != null).Select(seg => Convert(seg).Value).ToArray(),
                Condition: Convert((probe as LogProbe)?.When),
                Metric: Convert((probe as MetricProbe)?.Value));
        }

        internal ProbeInfo ProbeInfo { get; }

        private static DebuggerExpression? Convert(SnapshotSegment segment)
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
            if (ShouldCheckRateLimit(snapshotCreator) &&
                !ProbeRateLimiter.Instance.Sample(ProbeInfo.ProbeId))
            {
                return false;
            }

            ExpressionEvaluationResult evaluationResult = default;
            try
            {
                switch (info.MethodState)
                {
                    case MethodState.BeginLine:
                    case MethodState.BeginLineAsync:
                    case MethodState.EntryStart:
                    case MethodState.EntryAsync:
                        var captureBehaviour = snapshotCreator.DefineSnapshotBehavior(ref info, ProbeInfo.EvaluateAt, HasCondition());
                        if (captureBehaviour != CaptureBehaviour.Capture)
                        {
                            return true;
                        }

                        break;
                    case MethodState.ExitStart:
                    case MethodState.ExitStartAsync:
                        captureBehaviour = snapshotCreator.DefineSnapshotBehavior(ref info, ProbeInfo.EvaluateAt, HasCondition());
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
                            captureBehaviour = snapshotCreator.DefineSnapshotBehavior(ref info, ProbeInfo.EvaluateAt, HasCondition());
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
                                            evaluationResult = GetOrCreateEvaluator(snapshotCreator.MethodScopeMembers).Evaluate(snapshotCreator.MethodScopeMembers);
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
                        throw new ArgumentOutOfRangeException(
                            $"{nameof(info.MethodState)}",
                            $"{info.MethodState} is not valid value here");
                }

                return ProcessCapture(ref info, ref snapshotCreator, ref evaluationResult);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to process probe. Probe Id: " + ProbeInfo.ProbeId);
                return false;
            }
        }

        private bool ShouldCheckRateLimit(DebuggerSnapshotCreator snapshotCreator)
        {
            return (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Stop || (!HasCondition() && ProbeInfo.IsFullSnapshot));
        }

        private bool ProcessCapture<TCapture>(ref CaptureInfo<TCapture> info, ref DebuggerSnapshotCreator snapshotCreator, ref ExpressionEvaluationResult evaluationResult)
        {
            if (!ProbeRateLimiter.Instance.Sample(ProbeInfo.ProbeId))
            {
                return false;
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
                    throw new ArgumentOutOfRangeException($"{nameof(ProbeInfo.ProbeLocation)}", $"{info.MethodState} is not valid value here");
            }

            return true;
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
                        if (evaluationResult.IsNull())
                        {
                            throw new ArgumentException($"{nameof(evaluationResult)} can't be null when we are in {nameof(CaptureBehaviour.Evaluate)}", nameof(evaluationResult));
                        }

                        snapshotCreator.SetEvaluationResult(ref evaluationResult);
                    }

                    if (!ProbeInfo.IsFullSnapshot)
                    {
                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(ProbeInfo.ProbeId, ref info);
                        LiveDebugger.Instance.AddSnapshot(snapshot);
                        break;
                    }

                    snapshotCreator.ProcessDelayedSnapshot(ref info, HasCondition());
                    snapshotCreator.CaptureEntryMethodEndMarker(info.Value, info.Type, info.HasLocalOrArgument.Value);

                    break;
                case MethodState.ExitStart:
                case MethodState.ExitStartAsync:
                    snapshotCreator.CaptureExitMethodStartMarker(ref info);
                    break;
                case MethodState.ExitEnd:
                case MethodState.ExitEndAsync:
                    {
                        if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Evaluate)
                        {
                            if (evaluationResult.IsNull())
                            {
                                throw new ArgumentException($"{nameof(evaluationResult)} can't be null when we are in {nameof(CaptureBehaviour.Evaluate)}", nameof(evaluationResult));
                            }

                            snapshotCreator.SetEvaluationResult(ref evaluationResult);
                        }

                        if (ProbeInfo.IsFullSnapshot)
                        {
                            snapshotCreator.ProcessDelayedSnapshot(ref info, HasCondition());
                            snapshotCreator.CaptureExitMethodEndMarker(ref info);
                        }

                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(ProbeInfo.ProbeId, ref info);
                        LiveDebugger.Instance.AddSnapshot(snapshot);
                        snapshotCreator.Stop();
                        break;
                    }

                case MethodState.LogArg:
                    snapshotCreator.CaptureArgument(info.Value, info.Name, info.Type);
                    break;
                case MethodState.LogLocal:
                    snapshotCreator.CaptureLocal(info.Value, info.Name, info.Type);
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
                    if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Evaluate)
                    {
                        if (evaluationResult.IsNull())
                        {
                            throw new ArgumentException($"{nameof(evaluationResult)} can't be null when we are in {nameof(CaptureBehaviour.Evaluate)}", nameof(evaluationResult));
                        }

                        snapshotCreator.SetEvaluationResult(ref evaluationResult);
                    }

                    if (ProbeInfo.IsFullSnapshot)
                    {
                        snapshotCreator.ProcessDelayedSnapshot(ref info, HasCondition());
                        snapshotCreator.CaptureEndLine(ref info);
                    }

                    var snapshot = snapshotCreator.FinalizeLineSnapshot(ProbeInfo.ProbeId, ref info);
                    LiveDebugger.Instance.AddSnapshot(snapshot);
                    snapshotCreator.Stop();
                    break;

                case MethodState.LogArg:
                    snapshotCreator.CaptureArgument(info.Value, info.Name, info.Type);
                    break;
                case MethodState.LogLocal:
                    snapshotCreator.CaptureLocal(info.Value, info.Name, info.Type);
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
