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
    internal sealed class ProbeProcessor : IProbeProcessor
    {
        private const string DynamicPrefix = "_dd.di.";
        internal const int EvaluationErrorSnapshotRateLimitSeconds = 5 * 60;

        private static readonly long EvaluationErrorSnapshotRateLimitTicks = Stopwatch.Frequency * EvaluationErrorSnapshotRateLimitSeconds;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeProcessor));

        private readonly IDebuggerGlobalRateLimiter _globalRateLimiter;
        private volatile ProbeProcessorState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbeProcessor"/> class, that correlated to probe id
        /// </summary>
        /// <param name="probe">A probe that can pe log probe, metric probe or span decoration probe</param>
        /// <exception cref="ArgumentOutOfRangeException">If probe type or probe location is from unsupported type</exception>
        /// <remarks>Exceptions should be caught and logged by the caller</remarks>
        internal ProbeProcessor(ProbeDefinition probe)
            : this(probe, DebuggerGlobalRateLimiter.Instance)
        {
        }

        internal ProbeProcessor(ProbeDefinition probe, IDebuggerGlobalRateLimiter globalRateLimiter)
        {
            _globalRateLimiter = globalRateLimiter ?? throw new ArgumentNullException(nameof(globalRateLimiter));
            _state = ProbeProcessorState.Create(probe);
        }

        private static DebuggerExpression? ToDebuggerExpression(SnapshotSegment? segment)
        {
            return segment == null ? null : new DebuggerExpression(segment.Dsl, segment.Json?.ToString(), segment.Str);
        }

        public void LogException(Exception ex, IDebuggerSnapshotCreator snapshotCreator)
        {
        }

        public IProbeProcessor UpdateProbeProcessor(ProbeDefinition probe)
        {
            _state = ProbeProcessorState.Create(probe);
            return this;
        }

        private static CaptureLimitInfo ToCaptureLimitInfo(Capture? capture)
        {
            return capture != null
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
        }

        private static CaptureExpressionDefinition[]? CreateCaptureExpressions(CaptureExpression[]? captureExpressions)
        {
            if (captureExpressions == null || captureExpressions.Length == 0)
            {
                return null;
            }

            var result = new List<CaptureExpressionDefinition>(captureExpressions.Length);
            for (int i = 0; i < captureExpressions.Length; i++)
            {
                var captureExpression = captureExpressions[i];
                if (captureExpression?.Name is not { Length: > 0 } name)
                {
                    continue;
                }

                result.Add(new CaptureExpressionDefinition(
                    name,
                    ToDebuggerExpression(captureExpression.Expr),
                    ToCaptureLimitInfo(captureExpression.Capture)));
            }

            return result.Count == 0 ? null : result.ToArray();
        }

        private static bool ShouldRefreshMemoryPressureBeforeCapture(MethodState methodState)
        {
            return methodState is MethodState.BeginLine
                or MethodState.BeginLineAsync
                or MethodState.EntryStart
                or MethodState.EntryAsync
                or MethodState.ExitStart
                or MethodState.ExitStartAsync;
        }

        public bool TryBeginProcess(in ProbeData probeData, [NotNullWhen(true)] out IDebuggerSnapshotCreator? snapshotCreator)
        {
            var state = _state;
            if (!state.HasCondition && !SamplePayload(state.ProbeInfo, probeData.Sampler))
            {
                snapshotCreator = null;
                return false;
            }

            snapshotCreator = new DebuggerSnapshotCreator(state);
            return true;
        }

        private bool SamplePayload(in ProbeInfo probeInfo, IAdaptiveSampler sampler)
        {
            // Global-first matches Java; it can affect per-probe fairness and may be improved later.
            if (probeInfo.ProbeType == ProbeType.Snapshot && !_globalRateLimiter.ShouldSampleSnapshot(probeInfo.ProbeId))
            {
                return false;
            }

            return sampler.Sample();
        }

        public bool Process<TCapture>(ref CaptureInfo<TCapture> info, IDebuggerSnapshotCreator inSnapshotCreator, in ProbeData probeData)
        {
            if (inSnapshotCreator is not DebuggerSnapshotCreator { ProbeProcessorState: { } state } snapshotCreator)
            {
                throw new InvalidOperationException($"{nameof(ProbeProcessor)}.{nameof(Process)} requires a snapshot creator produced by {nameof(TryBeginProcess)}.");
            }

            var probeInfo = state.ProbeInfo;
            var dynamicInstrumentation = DebuggerManager.Instance.DynamicInstrumentation;
            if (dynamicInstrumentation is not null && ShouldRefreshMemoryPressureBeforeCapture(info.MethodState))
            {
                dynamicInstrumentation.RefreshMemoryPressureIfStale();
            }

            if (dynamicInstrumentation?.IsInitialized == false)
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
                        var captureBehaviour = snapshotCreator.DefineSnapshotBehavior(ref info, probeInfo.EvaluateAt, state.HasCondition);
                        if (captureBehaviour == CaptureBehaviour.Evaluate && info.IsAsyncCapture())
                        {
                            AddAsyncMethodArguments(snapshotCreator, ref info);
                            snapshotCreator.AddScopeMember(info.Name, info.Type, info.Value, info.MemberKind);
                            evaluationResult = Evaluate(state, probeInfo, snapshotCreator, out var shouldStopCapture, probeData.Sampler);
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
                        captureBehaviour = snapshotCreator.DefineSnapshotBehavior(ref info, probeInfo.EvaluateAt, state.HasCondition);
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
                            captureBehaviour = snapshotCreator.DefineSnapshotBehavior(ref info, probeInfo.EvaluateAt, state.HasCondition);
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
                                        evaluationResult = Evaluate(state, probeInfo, snapshotCreator, out var shouldStopCapture, probeData.Sampler);
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

                return ProcessCapture(state, probeInfo, ref info, snapshotCreator, ref evaluationResult);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to process probe. Probe Id: {ProbeId}", probeInfo.ProbeId);
                return false;
            }
            finally
            {
                snapshotCreator.StartSampling();
            }
        }

        private static void EvaluateCaptureExpressionsIfNeeded(
            ProbeProcessorState state,
            DebuggerSnapshotCreator snapshotCreator,
            ProbeExpressionsCacheEntry? cacheEntry,
            ref ProbeExpressionEvaluator? evaluator,
            ref ExpressionEvaluationResult evaluationResult,
            ref bool captureExpressionsEvaluated)
        {
            if (!state.ShouldCaptureExpressions || captureExpressionsEvaluated)
            {
                return;
            }

            evaluator ??= state.GetOrCreateEvaluator();
            evaluator.EvaluateCaptureExpressions(ref evaluationResult, snapshotCreator.MethodScopeMembers!, cacheEntry);
            captureExpressionsEvaluated = true;
        }

        private ExpressionEvaluationResult Evaluate(ProbeProcessorState state, ProbeInfo probeInfo, DebuggerSnapshotCreator snapshotCreator, out bool shouldStopCapture, IAdaptiveSampler sampler)
        {
            ExpressionEvaluationResult evaluationResult = default;
            shouldStopCapture = false;
            var captureExpressionsEvaluated = false;
            ProbeExpressionsCacheEntry? cacheEntry = null;
            ProbeExpressionEvaluator? evaluator = null;
            try
            {
                if (state.IsMetricCountWithoutExpression)
                {
                    evaluationResult = new ExpressionEvaluationResult { Metric = 1 };
                }
                else
                {
                    // we are taking the duration at the evaluation time - this might be different from what we have in the snapshot
                    snapshotCreator.SetDuration();
                    evaluator = state.GetOrCreateEvaluator();
                    evaluationResult = evaluator.Evaluate(snapshotCreator.MethodScopeMembers!, out cacheEntry);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to evaluate expression for probe: {ProbeId}", probeInfo.ProbeId);
                if (evaluationResult.IsNull())
                {
                    evaluationResult = new ExpressionEvaluationResult();
                }

                evaluationResult.Errors ??= new List<EvaluationError>();
                evaluationResult.Errors.Add(new EvaluationError { Message = $"Failed to evaluate expression for probe ID: {probeInfo.ProbeId}. Error: {e.Message}" });
            }

            if (state.ShouldCaptureExpressions && evaluationResult.IsNull())
            {
                EvaluateCaptureExpressionsIfNeeded(state, snapshotCreator, cacheEntry, ref evaluator, ref evaluationResult, ref captureExpressionsEvaluated);
            }

            if (captureExpressionsEvaluated && evaluationResult.IsNull())
            {
                shouldStopCapture = true;
                return evaluationResult;
            }

            if (evaluationResult.IsNull())
            {
                state.LogEvaluationState(snapshotCreator.MethodScopeMembers!);

                Log.Error("Evaluation result should not be null. Probe: {ProbeId}", probeInfo.ProbeId);
                evaluationResult.Errors = new List<EvaluationError> { new() { Message = $"Evaluation result is null. Probe ID: {probeInfo.ProbeId}" } };
            }

            if (Log.IsEnabled(LogEventLevel.Debug) && evaluationResult.Errors is { Count: > 0 } errors)
            {
                Log.Debug("Evaluation errors: {Errors}", errors.Select(er => $"Expression: {er.Expression}{Environment.NewLine}Error: {er.Message}"));
            }

            if (evaluationResult.Metric.HasValue && probeInfo.MetricKind.HasValue)
            {
                DebuggerManager.Instance.DynamicInstrumentation?.SendMetrics(probeInfo, probeInfo.MetricKind.Value, probeInfo.MetricName, evaluationResult.Metric.Value, probeInfo.ProbeId);
                // snapshot creator is created for all probes in the method invokers,
                // if it is a metric probe, once we sent the value, we can stop the invokers and dispose the snapshot creator
                snapshotCreator.Dispose();
                shouldStopCapture = true;
            }

            if (evaluationResult.Decorations != null)
            {
                SetSpanDecoration(state, in probeInfo, snapshotCreator, ref shouldStopCapture, evaluationResult);
            }

            if (evaluationResult.HasError)
            {
                // Condition evaluation errors bypass the per-probe sampler and global limiter.
                // A hard one-per-5-min cap guarantees at least one diagnostic snapshot per window without flooding.
                if (probeInfo.HasCondition && !state.ShouldSampleEvaluationErrorSnapshot())
                {
                    shouldStopCapture = true;
                }

                if (!shouldStopCapture)
                {
                    EvaluateCaptureExpressionsIfNeeded(state, snapshotCreator, cacheEntry, ref evaluator, ref evaluationResult, ref captureExpressionsEvaluated);
                }

                return evaluationResult;
            }

            if (evaluationResult.Condition != null && // i.e. not a metric, span probe, or span decoration
                (evaluationResult.Condition is false ||
                !SamplePayload(in probeInfo, sampler)))
            {
                // if the expression evaluated to false, or there is a rate limit, stop capture
                shouldStopCapture = true;
                return evaluationResult;
            }

            EvaluateCaptureExpressionsIfNeeded(state, snapshotCreator, cacheEntry, ref evaluator, ref evaluationResult, ref captureExpressionsEvaluated);

            return evaluationResult;
        }

        private void SetSpanDecoration(ProbeProcessorState state, in ProbeInfo probeInfo, DebuggerSnapshotCreator snapshotCreator, ref bool shouldStopCapture, ExpressionEvaluationResult evaluationResult)
        {
            var decorations = evaluationResult.Decorations;
            if (decorations == null)
            {
                return;
            }

            if (!TryGetScope(in probeInfo, out var scope))
            {
                Log.Debug("No active scope available, skipping span decoration. Probe: {ProbeId}", probeInfo.ProbeId);
                return;
            }

            var attachedTags = false;

            for (int i = 0; i < decorations.Length; i++)
            {
                var decoration = decorations[i];
                if (decoration.TagName is not { Length: > 0 } tagName)
                {
                    continue;
                }

                var evaluationErrorTag = $"{DynamicPrefix}{tagName}.evaluation_error";
                var probeIdTag = $"{DynamicPrefix}{tagName}.probe_id";
                ISpan? targetSpan = null;

                if (probeInfo.TargetSpan == null)
                {
                    Log.Error("We can't set the {Tag} tag. Probe ID: {ProbeId}, because target span is null", tagName, probeInfo.ProbeId);
                }

                switch (probeInfo.TargetSpan)
                {
                    case TargetSpan.Root:
                        targetSpan = scope.Root?.Span;
                        break;
                    case TargetSpan.Active:
                        targetSpan = scope.Span;
                        break;
                    default:
                        Log.Error("We can't set the {Tag} tag. Probe ID: {ProbeId}, because target span {Span} is invalid", tagName, probeInfo.ProbeId, probeInfo.TargetSpan);
                        break;
                }

                if (targetSpan == null)
                {
                    Log.Warning("We can't set the {Tag} tag. Probe ID: {ProbeId}, because the chosen span {Span} is not available", tagName, probeInfo.ProbeId, probeInfo.TargetSpan);
                    continue;
                }

                targetSpan.SetTag(tagName, decoration.Value);
                targetSpan.SetTag(probeIdTag, probeInfo.ProbeId);
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
                    Log.Debug("Successfully attached tag {Tag} to span {Span}. ProbID: {ProbeId}", tagName, targetSpan.SpanId, probeInfo.ProbeId);
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
                DebuggerManager.Instance.DynamicInstrumentation?.SetProbeStatusToEmitting(probeInfo);
            }
        }

        private bool TryGetScope(in ProbeInfo probeInfo, [NotNullWhen(true)] out Scope? scope)
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

                Log.Warning("Unable to find active scope in WCF context for span decoration. Probe ID: {ProbeId}", probeInfo.ProbeId);
                scope = null;
                return false;
#else
                Log.Warning("No active scope available for span decoration. Probe ID: {ProbeId}", probeInfo.ProbeId);
                scope = null;
                return false;
#endif
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while trying to get active scope for span decoration. Probe ID: {ProbeId}", probeInfo.ProbeId);
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

        private bool ProcessCapture<TCapture>(ProbeProcessorState state, ProbeInfo probeInfo, ref CaptureInfo<TCapture> info, DebuggerSnapshotCreator snapshotCreator, ref ExpressionEvaluationResult evaluationResult)
        {
            switch (probeInfo.ProbeLocation)
            {
                case ProbeLocation.Method:
                    ProcessMethod(state, in probeInfo, ref info, ref snapshotCreator, ref evaluationResult);
                    break;
                case ProbeLocation.Line:
                    ProcessLine(state, in probeInfo, ref info, ref snapshotCreator, ref evaluationResult);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"{nameof(ProbeInfo.ProbeLocation)}", $"{info.MethodState} is not valid value here");
            }

            return true;
        }

        private void ProcessMethod<TCapture>(ProbeProcessorState state, in ProbeInfo probeInfo, ref CaptureInfo<TCapture> info, ref DebuggerSnapshotCreator snapshotCreator, ref ExpressionEvaluationResult evaluationResult)
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
                        if (state.ShouldCaptureExpressions && evaluationResult.HasCaptureExpressions)
                        {
                            snapshotCreator.StartEntry();
                            snapshotCreator.CaptureCaptureExpressions(ref evaluationResult);
                            snapshotCreator.EndEntry();
                        }
                    }

                    if (!probeInfo.IsFullSnapshot)
                    {
                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(probeInfo.ProbeId, probeInfo.ProbeVersion, ref info);
                        DebuggerManager.Instance.DynamicInstrumentation?.AddSnapshot(probeInfo, snapshot);
                        break;
                    }

                    if (!snapshotCreator.ProcessDelayedSnapshot(ref info, state.HasCondition))
                    {
                        snapshotCreator.CaptureEntryAsyncMethod(ref info);
                    }

                    break;
                case MethodState.EntryEnd:
                    if (snapshotCreator.CaptureBehaviour == CaptureBehaviour.Evaluate)
                    {
                        snapshotCreator.SetEvaluationResult(ref evaluationResult);
                        if (state.ShouldCaptureExpressions && evaluationResult.HasCaptureExpressions)
                        {
                            snapshotCreator.StartEntry();
                            snapshotCreator.CaptureCaptureExpressions(ref evaluationResult);
                            snapshotCreator.EndEntry();
                        }
                    }

                    if (!probeInfo.IsFullSnapshot)
                    {
                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(probeInfo.ProbeId, probeInfo.ProbeVersion, ref info);
                        DebuggerManager.Instance.DynamicInstrumentation?.AddSnapshot(probeInfo, snapshot);
                        break;
                    }

                    snapshotCreator.ProcessDelayedSnapshot(ref info, state.HasCondition);
                    snapshotCreator.CaptureEntryMethodEndMarker(info.Value, info.Type);

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
                            if (state.ShouldCaptureExpressions && evaluationResult.HasCaptureExpressions)
                            {
                                snapshotCreator.StartReturn();
                                snapshotCreator.CaptureCaptureExpressions(ref evaluationResult);
                                snapshotCreator.EndReturn();
                            }
                        }

                        if (probeInfo.IsFullSnapshot)
                        {
                            snapshotCreator.ProcessDelayedSnapshot(ref info, state.HasCondition);
                            snapshotCreator.CaptureExitMethodEndMarker(ref info);
                        }

                        var snapshot = snapshotCreator.FinalizeMethodSnapshot(probeInfo.ProbeId, probeInfo.ProbeVersion, ref info);
                        DebuggerManager.Instance.DynamicInstrumentation?.AddSnapshot(probeInfo, snapshot);
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

        private void ProcessLine<TCapture>(ProbeProcessorState state, in ProbeInfo probeInfo, ref CaptureInfo<TCapture> info, ref DebuggerSnapshotCreator snapshotCreator, ref ExpressionEvaluationResult evaluationResult)
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
                        if (state.ShouldCaptureExpressions && evaluationResult.HasCaptureExpressions)
                        {
                            snapshotCreator.StartLines(info.LineCaptureInfo.LineNumber);
                            snapshotCreator.CaptureCaptureExpressions(ref evaluationResult);
                            snapshotCreator.EndReturn();
                        }
                    }

                    if (probeInfo.IsFullSnapshot)
                    {
                        snapshotCreator.ProcessDelayedSnapshot(ref info, state.HasCondition);
                        snapshotCreator.CaptureEndLine(ref info);
                    }

                    var snapshot = snapshotCreator.FinalizeLineSnapshot(probeInfo.ProbeId, probeInfo.ProbeVersion, ref info);
                    DebuggerManager.Instance.DynamicInstrumentation?.AddSnapshot(probeInfo, snapshot);
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

        internal sealed class ProbeProcessorState
        {
            private ProbeExpressionEvaluator? _evaluator;
            private long _lastEvaluationErrorSnapshotTimestamp = -EvaluationErrorSnapshotRateLimitTicks;

            private ProbeProcessorState(
                ProbeInfo probeInfo,
                DebuggerExpression?[]? templates,
                DebuggerExpression? condition,
                DebuggerExpression? metric,
                KeyValuePair<DebuggerExpression?, KeyValuePair<string?, DebuggerExpression?[]>[]>[]? spanDecorations,
                CaptureExpressionDefinition[]? captureExpressions)
            {
                ProbeInfo = probeInfo;
                Templates = templates;
                Condition = condition;
                Metric = metric;
                SpanDecorations = spanDecorations;
                CaptureExpressions = captureExpressions;
                HasCondition = condition.HasValue;
                IsMetricCountWithoutExpression = probeInfo.ProbeType == ProbeType.Metric && (metric?.Json == null) && probeInfo.MetricKind == MetricKind.COUNT;
                ShouldCaptureExpressions = !probeInfo.IsFullSnapshot && captureExpressions is { Length: > 0 };
            }

            internal ProbeInfo ProbeInfo { get; }

            private DebuggerExpression?[]? Templates { get; }

            private DebuggerExpression? Condition { get; }

            private DebuggerExpression? Metric { get; }

            private KeyValuePair<DebuggerExpression?, KeyValuePair<string?, DebuggerExpression?[]>[]>[]? SpanDecorations { get; }

            private CaptureExpressionDefinition[]? CaptureExpressions { get; }

            internal bool HasCondition { get; }

            internal bool IsMetricCountWithoutExpression { get; }

            internal bool ShouldCaptureExpressions { get; }

            internal static ProbeProcessorState Create(ProbeDefinition probe)
            {
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

                var templates = default(DebuggerExpression?[]?);
                DebuggerExpression? condition = null;
                DebuggerExpression? metric = null;
                KeyValuePair<DebuggerExpression?, KeyValuePair<string?, DebuggerExpression?[]>[]>[]? spanDecorations = null;
                CaptureExpressionDefinition[]? captureExpressions = null;

                // ReSharper disable once PossibleInvalidOperationException
                switch (probe)
                {
                    case LogProbe logProbe:
                        templates = logProbe.Segments?.Select(ToDebuggerExpression).ToArray();
                        condition = ToDebuggerExpression(logProbe.When);
                        captureExpressions = logProbe.CaptureSnapshot ? null : CreateCaptureExpressions(logProbe.CaptureExpressions);
                        break;
                    case MetricProbe metricProbe:
                        metric = ToDebuggerExpression(metricProbe.Value);
                        break;
                    case SpanDecorationProbe spanDecorationProbe:
                        spanDecorations = spanDecorationProbe.
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

                var probeInfo = new ProbeInfo(
                    probe.Id,
                    probe.Version ?? 0,
                    probeType,
                    location,
                    evaluateAt,
                    (probe as MetricProbe)?.Kind,
                    (probe as MetricProbe)?.MetricName,
                    condition.HasValue,
                    probe.Tags,
                    (probe as SpanDecorationProbe)?.TargetSpan,
                    ToCaptureLimitInfo((probe as LogProbe)?.Capture));

                return new ProbeProcessorState(
                    probeInfo,
                    templates,
                    condition,
                    metric,
                    spanDecorations,
                    captureExpressions);
            }

            internal ProbeExpressionEvaluator GetOrCreateEvaluator()
            {
                var evaluator = Volatile.Read(ref _evaluator);
                if (evaluator != null)
                {
                    return evaluator;
                }

                var newEvaluator = new ProbeExpressionEvaluator(Templates, Condition, Metric, SpanDecorations, CaptureExpressions);
                var previousEvaluator = Interlocked.CompareExchange(ref _evaluator, newEvaluator, null);
                return previousEvaluator ?? newEvaluator;
            }

            internal bool ShouldSampleEvaluationErrorSnapshot()
            {
                var timestamp = Stopwatch.GetTimestamp();
                var lastTimestamp = Volatile.Read(ref _lastEvaluationErrorSnapshotTimestamp);
                if (timestamp - lastTimestamp < EvaluationErrorSnapshotRateLimitTicks)
                {
                    return false;
                }

                return Interlocked.CompareExchange(ref _lastEvaluationErrorSnapshotTimestamp, timestamp, lastTimestamp) == lastTimestamp;
            }

            internal void LogEvaluationState(MethodScopeMembers methodScopeMembers)
            {
                if (Templates == null
                 && Condition == null
                 && Metric == null
                 && SpanDecorations == null
                 && CaptureExpressions == null)
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
                        if (members?.Count > 0)
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
        }
    }
}
