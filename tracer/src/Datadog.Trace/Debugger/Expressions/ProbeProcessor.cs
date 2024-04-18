// <copyright file="ProbeProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Expressions
{
    internal class ProbeProcessor : IProbeProcessor
    {
        private const string DynamicPrefix = "_dd.di.";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeProcessor));

        private ProbeExpressionEvaluator _evaluator;
        private DebuggerExpression[] _templates;
        private DebuggerExpression? _condition;
        private DebuggerExpression? _metric;
        private KeyValuePair<DebuggerExpression?, KeyValuePair<string, DebuggerExpression[]>[]>[] _spanDecorations;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbeProcessor"/> class, that correlated to probe id
        /// </summary>
        /// <param name="probe">A probe that can pe log probe, metric probe or span decoration probe</param>
        /// <exception cref="ArgumentOutOfRangeException">If probe type or probe location is from unsupported type</exception>
        /// <remarks>Exceptions should be caught and logged by the caller</remarks>
        internal ProbeProcessor(ProbeDefinition probe)
        {
            InitializeProbeProcessor(probe);
        }

        internal ProbeInfo ProbeInfo { get; private set; }

        private bool IsMetricCountWithoutExpression => ProbeInfo.ProbeType == ProbeType.Metric && (_metric?.Json == null) && ProbeInfo.MetricKind == MetricKind.COUNT;

        private void InitializeProbeProcessor(ProbeDefinition probe)
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
                SpanDecorationProbe => ProbeType.SpanDecoration,
                _ => throw new ArgumentOutOfRangeException(nameof(probe), probe, "Unsupported probe type")
            };

            var evaluateAt = location switch
            {
                ProbeLocation.Method => probe.EvaluateAt,
                ProbeLocation.Line => EvaluateAt.Entry,
                _ => throw new ArgumentOutOfRangeException(nameof(location), location, "Unsupported probe location")
            };

            SetExpressions(probe);

            var capture = (probe as LogProbe)?.Capture;
            var maxInfo = capture != null
                ? new CaptureLimitInfo(
                    MaxReferenceDepth: capture.Value.MaxReferenceDepth <= 0 ? DebuggerSettings.DefaultMaxDepthToSerialize : capture.Value.MaxReferenceDepth,
                    MaxCollectionSize: capture.Value.MaxCollectionSize <= 0 ? DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy : capture.Value.MaxCollectionSize,
                    MaxFieldCount: capture.Value.MaxFieldCount <= 0 ? DebuggerSettings.DefaultMaxNumberOfFieldsToCopy : capture.Value.MaxFieldCount,
                    MaxLength: capture.Value.MaxLength <= 0 ? DebuggerSettings.DefaultMaxStringLength : capture.Value.MaxLength)
                : new CaptureLimitInfo(
                    MaxReferenceDepth: DebuggerSettings.DefaultMaxDepthToSerialize,
                    MaxCollectionSize: DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
                    MaxFieldCount: DebuggerSettings.DefaultMaxNumberOfFieldsToCopy,
                    MaxLength: DebuggerSettings.DefaultMaxStringLength);

            ProbeInfo = new ProbeInfo(
                probe.Id,
                probe.Version ?? 0,
                probeType,
                location,
                evaluateAt,
                (probe as MetricProbe)?.Kind,
                (probe as MetricProbe)?.MetricName,
                HasCondition(),
                probe.Tags,
                (probe as SpanDecorationProbe)?.TargetSpan,
                maxInfo);
        }

        [DebuggerStepThrough]
        private bool HasCondition() => _condition.HasValue;

        private DebuggerExpression? ToDebuggerExpression(SnapshotSegment segment)
        {
            return segment == null ? null : new DebuggerExpression(segment.Dsl, segment.Json?.ToString(), segment.Str);
        }

        public void LogException(Exception ex, IDebuggerSnapshotCreator snapshotCreator)
        {
        }

        public IProbeProcessor UpdateProbeProcessor(ProbeDefinition probe)
        {
            InitializeProbeProcessor(probe);
            return this;
        }

        public IDebuggerSnapshotCreator CreateSnapshotCreator()
        {
            return new DebuggerSnapshotCreator(ProbeInfo.IsFullSnapshot, ProbeInfo.ProbeLocation, ProbeInfo.HasCondition, ProbeInfo.Tags, ProbeInfo.CaptureLimitInfo);
        }

        private void SetExpressions(ProbeDefinition probe)
        {
            // ReSharper disable once PossibleInvalidOperationException
            switch (probe)
            {
                case LogProbe logProbe:
                    _templates = logProbe.Segments?.Select(seg => ToDebuggerExpression(seg).Value).ToArray();
                    _condition = ToDebuggerExpression(logProbe.When);
                    break;
                case MetricProbe metricProbe:
                    _metric = ToDebuggerExpression(metricProbe.Value);
                    break;
                case SpanDecorationProbe spanDecorationProbe:
                    _spanDecorations = spanDecorationProbe.
                        Decorations
                      ?.Where(dec => dec != null)
                       .Select(
                            dec =>
                            {
                                var whenExpression = ToDebuggerExpression(dec.When);
                                var keyValuePairs = dec.Tags?.Select(
                                                            tag => new KeyValuePair<string, DebuggerExpression[]>(
                                                                tag.Name,
                                                                tag.Value?.Segments?.Select(seg => ToDebuggerExpression(seg).Value).ToArray()))
                                                       .ToArray();

                                return new KeyValuePair<DebuggerExpression?, KeyValuePair<string, DebuggerExpression[]>[]>(whenExpression, keyValuePairs);
                            })
                       .ToArray();

                    break;
            }
        }

        private ProbeExpressionEvaluator GetOrCreateEvaluator()
        {
            Interlocked.CompareExchange(ref _evaluator, new ProbeExpressionEvaluator(_templates, _condition, _metric, _spanDecorations), null);
            return _evaluator;
        }

        public bool ShouldProcess(in ProbeData probeData)
        {
            return HasCondition() || probeData.Sampler.Sample();
        }

        public bool Process<TCapture>(ref CaptureInfo<TCapture> info, IDebuggerSnapshotCreator inSnapshotCreator, in ProbeData probeData)
        {
            var snapshotCreator = (DebuggerSnapshotCreator)inSnapshotCreator;

            ExpressionEvaluationResult evaluationResult = default;
            try
            {
                if (info.MethodState is not (MethodState.BeginLine or MethodState.BeginLineAsync or MethodState.EntryStart or MethodState.EntryAsync))
                {
                    snapshotCreator.StopSampling();
                }

                switch (info.MethodState)
                {
                    case MethodState.BeginLine:
                    case MethodState.BeginLineAsync:
                    case MethodState.EntryStart:
                    case MethodState.EntryAsync:
                        var captureBehaviour = snapshotCreator.DefineSnapshotBehavior(ref info, ProbeInfo.EvaluateAt, HasCondition());
                        if (captureBehaviour == CaptureBehaviour.Evaluate && info.IsAsyncCapture())
                        {
                            AddAsyncMethodArguments(snapshotCreator, ref info);
                            snapshotCreator.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                            evaluationResult = Evaluate(snapshotCreator, out var shouldStopCapture, probeData.Sampler);
                            if (shouldStopCapture)
                            {
                                snapshotCreator.Stop();
                                return false;
                            }

                            break;
                        }

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
                                if (info.IsAsyncCapture())
                                {
                                    AddAsyncMethodArguments(snapshotCreator, ref info);
                                    AddAsyncMethodLocals(snapshotCreator, ref info);
                                }

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
                                        if (info.IsAsyncCapture())
                                        {
                                            AddAsyncMethodArguments(snapshotCreator, ref info);
                                            AddAsyncMethodLocals(snapshotCreator, ref info);
                                        }

                                        snapshotCreator.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                                        evaluationResult = Evaluate(snapshotCreator, out var shouldStopCapture, probeData.Sampler);
                                        if (shouldStopCapture)
                                        {
                                            snapshotCreator.Stop();
                                            return false;
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

                return ProcessCapture(ref info, snapshotCreator, ref evaluationResult);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to process probe. Probe Id: {ProbeId}", ProbeInfo.ProbeId);
                return false;
            }
            finally
            {
                snapshotCreator.StartSampling();
            }
        }

        private ExpressionEvaluationResult Evaluate(DebuggerSnapshotCreator snapshotCreator, out bool shouldStopCapture, IAdaptiveSampler sampler)
        {
            ExpressionEvaluationResult evaluationResult = default;
            shouldStopCapture = false;
            try
            {
                if (IsMetricCountWithoutExpression)
                {
                    evaluationResult = new ExpressionEvaluationResult { Metric = 1 };
                }
                else
                {
                    // we are taking the duration at the evaluation time - this might be different from what we have in the snapshot
                    snapshotCreator.SetDuration();
                    evaluationResult = GetOrCreateEvaluator().Evaluate(snapshotCreator.MethodScopeMembers);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to evaluate expression for probe: {ProbeId}", ProbeInfo.ProbeId);
                if (evaluationResult.IsNull())
                {
                    evaluationResult = new ExpressionEvaluationResult();
                }

                evaluationResult.Errors ??= new List<EvaluationError>();
                evaluationResult.Errors.Add(new EvaluationError { Message = $"Failed to evaluate expression for probe ID: {ProbeInfo.ProbeId}. Error: {e.Message}" });
                return evaluationResult;
            }

            if (evaluationResult.IsNull())
            {
                Log.Error("Evaluation result should not be null. Probe: {ProbeId}", ProbeInfo.ProbeId);
                evaluationResult.Errors = new List<EvaluationError> { new() { Message = $"Evaluation result is null. Probe ID: {ProbeInfo.ProbeId}" } };
                return evaluationResult;
            }

            CheckSpanDecoration(snapshotCreator, ref shouldStopCapture, evaluationResult);

            if (evaluationResult.Metric.HasValue)
            {
                LiveDebugger.Instance.SendMetrics(ProbeInfo, ProbeInfo.MetricKind.Value, ProbeInfo.MetricName, evaluationResult.Metric.Value, ProbeInfo.ProbeId);
                // snapshot creator is created for all probes in the method invokers,
                // if it is a metric probe, once we sent the value, we can stop the invokers and dispose the snapshot creator
                snapshotCreator.Dispose();
                shouldStopCapture = true;
            }

            if (evaluationResult.HasError)
            {
                return evaluationResult;
            }

            if (evaluationResult.Condition != null && // meaning not metric, span probe or span decoration
                (evaluationResult.Condition is false ||
                !sampler.Sample()))
            {
                // if the expression evaluated to false, or there is a rate limit, stop capture
                shouldStopCapture = true;
                return evaluationResult;
            }

            return evaluationResult;
        }

        private void CheckSpanDecoration(DebuggerSnapshotCreator snapshotCreator, ref bool shouldStopCapture, ExpressionEvaluationResult evaluationResult)
        {
            if (evaluationResult.Decorations == null)
            {
                return;
            }

            var attachedTags = false;

            for (int i = 0; i < evaluationResult.Decorations.Length; i++)
            {
                var decoration = evaluationResult.Decorations[i];
                var evaluationErrorTag = $"{DynamicPrefix}{decoration.TagName}.evaluation_error";
                var probeIdTag = $"{DynamicPrefix}{decoration.TagName}.probe_id";
                switch (ProbeInfo.TargetSpan)
                {
                    case TargetSpan.Root:
                        Tracer.Instance.ScopeManager.Active.Root.Span.SetTag(decoration.TagName, decoration.Value);
                        Tracer.Instance.ScopeManager.Active.Root.Span.SetTag(probeIdTag, ProbeInfo.ProbeId);
                        if (decoration.Errors?.Length > 0)
                        {
                            Tracer.Instance.ScopeManager.Active.Root.Span.SetTag(evaluationErrorTag, string.Join(";", decoration.Errors));
                        }
                        else if (Tracer.Instance.ScopeManager.Active.Span.GetTag(evaluationErrorTag) != null)
                        {
                            Tracer.Instance.ScopeManager.Active.Root.Span.SetTag(evaluationErrorTag, null);
                        }

                        attachedTags = true;

                        break;
                    case TargetSpan.Active:
                        Tracer.Instance.ScopeManager.Active.Span.SetTag(decoration.TagName, decoration.Value);
                        Tracer.Instance.ScopeManager.Active.Span.SetTag(probeIdTag, ProbeInfo.ProbeId);
                        if (decoration.Errors?.Length > 0)
                        {
                            Tracer.Instance.ScopeManager.Active.Span.SetTag(evaluationErrorTag, string.Join(";", decoration.Errors));
                        }
                        else if (Tracer.Instance.ScopeManager.Active.Span.GetTag(evaluationErrorTag) != null)
                        {
                            Tracer.Instance.ScopeManager.Active.Span.SetTag(evaluationErrorTag, null);
                        }

                        attachedTags = true;

                        break;
                    default:
                        Log.Error("Invalid target span. Probe: {ProbeId}", ProbeInfo.ProbeId);
                        break;
                }
            }

            // once we added the tags, we can stop the invokers and dispose the snapshot creator
            if (!evaluationResult.HasError)
            {
                snapshotCreator.Dispose();
                shouldStopCapture = true;
            }

            if (attachedTags)
            {
                LiveDebugger.Instance.SetProbeStatusToEmitting(ProbeInfo);
            }
        }

        internal static void AddAsyncMethodArguments<T>(DebuggerSnapshotCreator snapshotCreator, ref CaptureInfo<T> captureInfo)
        {
            var asyncCaptureInfo = captureInfo.AsyncCaptureInfo;
            for (int i = 0; i < asyncCaptureInfo.HoistedArguments.Length; i++)
            {
                var arg = asyncCaptureInfo.HoistedArguments[i];
                if (arg == default)
                {
                    continue;
                }

                var argValue = arg.GetValue(asyncCaptureInfo.MoveNextInvocationTarget);
                snapshotCreator.AddScopeMember(arg.Name, argValue?.GetType() ?? arg.FieldType, argValue, ScopeMemberKind.Argument);
            }
        }

        internal static void AddAsyncMethodLocals<T>(DebuggerSnapshotCreator snapshotCreator, ref CaptureInfo<T> captureInfo)
        {
            var asyncCaptureInfo = captureInfo.AsyncCaptureInfo;
            for (int i = 0; i < asyncCaptureInfo.HoistedLocals.Length; i++)
            {
                var local = asyncCaptureInfo.HoistedLocals[i];
                if (local == default)
                {
                    continue;
                }

                var localValue = local.Field.GetValue(asyncCaptureInfo.MoveNextInvocationTarget);
                snapshotCreator.AddScopeMember(local.SanitizedName, localValue?.GetType() ?? local.Field.FieldType, localValue, ScopeMemberKind.Local);
            }
        }

        private bool ProcessCapture<TCapture>(ref CaptureInfo<TCapture> info, DebuggerSnapshotCreator snapshotCreator, ref ExpressionEvaluationResult evaluationResult)
        {
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
                    if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Evaluate)
                    {
                        snapshotCreator.SetEvaluationResult(ref evaluationResult);
                    }

                    if (!ProbeInfo.IsFullSnapshot)
                    {
                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(ProbeInfo.ProbeId, ProbeInfo.ProbeVersion, ref info);
                        LiveDebugger.Instance.AddSnapshot(ProbeInfo, snapshot);
                        break;
                    }

                    if (!snapshotCreator.ProcessDelayedSnapshot(ref info, HasCondition()))
                    {
                        snapshotCreator.CaptureEntryAsyncMethod(ref info);
                    }

                    break;
                case MethodState.EntryEnd:
                    if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Evaluate)
                    {
                        snapshotCreator.SetEvaluationResult(ref evaluationResult);
                    }

                    if (!ProbeInfo.IsFullSnapshot)
                    {
                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(ProbeInfo.ProbeId, ProbeInfo.ProbeVersion, ref info);
                        LiveDebugger.Instance.AddSnapshot(ProbeInfo, snapshot);
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
                            snapshotCreator.SetEvaluationResult(ref evaluationResult);
                        }

                        if (ProbeInfo.IsFullSnapshot)
                        {
                            snapshotCreator.ProcessDelayedSnapshot(ref info, HasCondition());
                            snapshotCreator.CaptureExitMethodEndMarker(ref info);
                        }

                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(ProbeInfo.ProbeId, ProbeInfo.ProbeVersion, ref info);
                        LiveDebugger.Instance.AddSnapshot(ProbeInfo, snapshot);
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
                        snapshotCreator.SetEvaluationResult(ref evaluationResult);
                    }

                    if (ProbeInfo.IsFullSnapshot)
                    {
                        snapshotCreator.ProcessDelayedSnapshot(ref info, HasCondition());
                        snapshotCreator.CaptureEndLine(ref info);
                    }

                    var snapshot = snapshotCreator.FinalizeLineSnapshot(ProbeInfo.ProbeId, ProbeInfo.ProbeVersion, ref info);
                    LiveDebugger.Instance.AddSnapshot(ProbeInfo, snapshot);
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
    }
}
