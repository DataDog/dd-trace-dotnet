// <copyright file="ProbeProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Wcf;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.Expressions
{
    internal class ProbeProcessor : IProbeProcessor
    {
        private const string DynamicPrefix = "_dd.di.";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeProcessor));

        private ProbeExpressionEvaluator? _evaluator;
        private DebuggerExpression?[]? _templates;
        private DebuggerExpression? _condition;
        private DebuggerExpression? _metric;
        private KeyValuePair<DebuggerExpression?, KeyValuePair<string?, DebuggerExpression?[]>[]>[]? _spanDecorations;

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
            _evaluator = null;
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

        private DebuggerExpression? ToDebuggerExpression(SnapshotSegment? segment)
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
            return new DebuggerSnapshotCreator(ProbeInfo.IsFullSnapshot, ProbeInfo.ProbeLocation, ProbeInfo.HasCondition, ProbeInfo.Tags, ProbeInfo.CaptureLimitInfo, Tracer.Instance.Settings.PropagateProcessTags);
        }

        private void SetExpressions(ProbeDefinition probe)
        {
            // ReSharper disable once PossibleInvalidOperationException
            switch (probe)
            {
                case LogProbe logProbe:
                    _templates = logProbe.Segments?.Select(ToDebuggerExpression).ToArray();
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
                                                            tag => new KeyValuePair<string?, DebuggerExpression?[]>(
                                                                tag.Name,
                                                                tag.Value?.Segments?.Select(ToDebuggerExpression).ToArray() ?? []))
                                                       .ToArray();

                                return new KeyValuePair<DebuggerExpression?, KeyValuePair<string?, DebuggerExpression?[]>[]>(whenExpression, keyValuePairs ?? []);
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

            if (DebuggerManager.Instance.DynamicInstrumentation?.IsInitialized == false)
            {
                Log.Debug("Stop processing probe {ID} because Dynamic Instrumentation has not initialized yet or has been disabled, probably dynamically through Remote Config", probeData.ProbeId);
                snapshotCreator.Stop();
                return false;
            }

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
                LogEvaluationState(snapshotCreator.MethodScopeMembers);

                Log.Error("Evaluation result should not be null. Probe: {ProbeId}", ProbeInfo.ProbeId);
                evaluationResult.Errors = new List<EvaluationError> { new() { Message = $"Evaluation result is null. Probe ID: {ProbeInfo.ProbeId}" } };
                return evaluationResult;
            }

            if (Log.IsEnabled(LogEventLevel.Debug) && evaluationResult.HasError)
            {
                Log.Debug("Evaluation errors: {Errors}", evaluationResult.Errors.Select(er => $"Expression: {er.Expression}{Environment.NewLine}Error: {er.Message}"));
            }

            if (evaluationResult.Metric.HasValue && ProbeInfo.MetricKind.HasValue)
            {
                DebuggerManager.Instance.DynamicInstrumentation?.SendMetrics(ProbeInfo, ProbeInfo.MetricKind.Value, ProbeInfo.MetricName, evaluationResult.Metric.Value, ProbeInfo.ProbeId);
                // snapshot creator is created for all probes in the method invokers,
                // if it is a metric probe, once we sent the value, we can stop the invokers and dispose the snapshot creator
                snapshotCreator.Dispose();
                shouldStopCapture = true;
            }

            if (evaluationResult.Decorations != null)
            {
                SetSpanDecoration(snapshotCreator, ref shouldStopCapture, evaluationResult);
            }

            if (evaluationResult.HasError)
            {
                return evaluationResult;
            }

            if (evaluationResult.Condition != null && // i.e. not a metric, span probe, or span decoration
                (evaluationResult.Condition is false ||
                !sampler.Sample()))
            {
                // if the expression evaluated to false, or there is a rate limit, stop capture
                shouldStopCapture = true;
                return evaluationResult;
            }

            return evaluationResult;
        }

        private void LogEvaluationState(MethodScopeMembers methodScopeMembers)
        {
            if (_templates == null
             && _condition == null
             && _metric == null
             && _spanDecorations == null)
            {
                return;
            }

            // this should never happen, but if it does, we want to log it for further investigation
            Log.Error("Evaluation state error: we thought that evaluation result is null but that not true. probably we are using an incorrect version of ProbeExpressionEvaluator");

            try
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    var instance = methodScopeMembers.InvocationTarget;
                    var members = methodScopeMembers.Members?.Select(m => new { Name = m.Name, Type = m.Type?.FullName ?? m.Type?.Name }).ToList();
                    string? membersAsString = null;
                    if (members?.Any() == true)
                    {
                        membersAsString = string.Join(";", members);
                    }

                    Log.Error("Evaluation state error: Target Method: Type = {Type}, Name = {Name}. Method Members: {Members}", instance.Type?.FullName ?? instance.Type?.Name, instance.Name, membersAsString);
                }
            }
            catch
            {
                // ignored
            }
        }

        private void SetSpanDecoration(DebuggerSnapshotCreator snapshotCreator, ref bool shouldStopCapture, ExpressionEvaluationResult evaluationResult)
        {
            if (!TryGetScope(out var scope))
            {
                Log.Debug("No active scope available, skipping span decoration. Probe: {ProbeId}", ProbeInfo.ProbeId);
                return;
            }

            var attachedTags = false;

            for (int i = 0; i < evaluationResult.Decorations.Length; i++)
            {
                var decoration = evaluationResult.Decorations[i];
                var evaluationErrorTag = $"{DynamicPrefix}{decoration.TagName}.evaluation_error";
                var probeIdTag = $"{DynamicPrefix}{decoration.TagName}.probe_id";
                ISpan? targetSpan = null;

                if (ProbeInfo.TargetSpan == null)
                {
                    Log.Error("We can't set the {Tag} tag. Probe ID: {ProbeId}, because target span is null", decoration.TagName, ProbeInfo.ProbeId);
                }

                switch (ProbeInfo.TargetSpan)
                {
                    case TargetSpan.Root:
                        targetSpan = scope.Root?.Span;
                        break;
                    case TargetSpan.Active:
                        targetSpan = scope.Span;
                        break;
                    default:
                        Log.Error("We can't set the {Tag} tag. Probe ID: {ProbeId}, because target span {Span} is invalid", decoration.TagName, ProbeInfo.ProbeId, ProbeInfo.TargetSpan);
                        break;
                }

                if (targetSpan == null)
                {
                    Log.Warning("We can't set the {Tag} tag. Probe ID: {ProbeId}, because the chosen span {Span} is not available", decoration.TagName, ProbeInfo.ProbeId, ProbeInfo.TargetSpan);
                    continue;
                }

                targetSpan.SetTag(decoration.TagName, decoration.Value);
                targetSpan.SetTag(probeIdTag, ProbeInfo.ProbeId);
                if (decoration.Errors?.Length > 0)
                {
                    targetSpan.SetTag(evaluationErrorTag, string.Join(";", decoration.Errors));
                }
                else if (targetSpan.GetTag(evaluationErrorTag) != null)
                {
                    targetSpan.SetTag(evaluationErrorTag, null);
                }

                attachedTags = true;
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Successfully attached tag {Tag} to span {Span}. ProbID: {ProbeId}", decoration.TagName, targetSpan.SpanId, ProbeInfo.ProbeId);
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
                DebuggerManager.Instance.DynamicInstrumentation?.SetProbeStatusToEmitting(ProbeInfo);
            }
        }

        private bool TryGetScope([NotNullWhen(true)] out Scope? scope)
        {
            try
            {
                if (Tracer.Instance.ActiveScope is Scope activeScope)
                {
                    scope = activeScope;
                    return true;
                }
#if NETFRAMEWORK
                var ctx = WcfCommon.GetCurrentOperationContext?.Invoke();
                if (ctx?.DuckCast<IOperationContextStruct>() is { } ctxProxy
                 && ((IDuckType?)ctxProxy.RequestContext)?.Instance is { } requestContextInstance
                 && WcfCommon.Scopes.TryGetValue(requestContextInstance, out scope))
                {
                    return scope != null;
                }

                Log.Warning("Unable to find active scope in WCF context for span decoration. Probe ID: {ProbeId}", ProbeInfo.ProbeId);
                scope = null;
                return false;
#else
                Log.Warning("No active scope available for span decoration. Probe ID: {ProbeId}", ProbeInfo.ProbeId);
                scope = null;
                return false;
#endif
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while trying to get active scope for span decoration. Probe ID: {ProbeId}", ProbeInfo.ProbeId);
                scope = null;
                return false;
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
                        DebuggerManager.Instance.DynamicInstrumentation?.AddSnapshot(ProbeInfo, snapshot);
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
                        DebuggerManager.Instance.DynamicInstrumentation?.AddSnapshot(ProbeInfo, snapshot);
                        break;
                    }

                    snapshotCreator.ProcessDelayedSnapshot(ref info, HasCondition());
                    snapshotCreator.CaptureEntryMethodEndMarker(info.Value, info.Type, info.HasLocalOrArgument ?? false);

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
                        DebuggerManager.Instance.DynamicInstrumentation?.AddSnapshot(ProbeInfo, snapshot);
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
                    DebuggerManager.Instance.DynamicInstrumentation?.AddSnapshot(ProbeInfo, snapshot);
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
