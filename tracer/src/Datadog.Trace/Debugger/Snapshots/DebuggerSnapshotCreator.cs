// <copyright file="DebuggerSnapshotCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using ProbeLocation = Datadog.Trace.Debugger.Expressions.ProbeLocation;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal class DebuggerSnapshotCreator : IDebuggerSnapshotCreator, IDisposable
    {
        private const string LoggerVersion = "2";
        private const string UnknownValue = "Unknown";

#pragma warning disable SA1401
        protected readonly JsonTextWriter JsonWriter;
#pragma warning restore SA1401
        private readonly StringBuilder _jsonUnderlyingString;
        private readonly bool _isFullSnapshot;
        private readonly ProbeLocation _probeLocation;
        private readonly CaptureLimitInfo _limitInfo;
        private readonly bool _injectProcessTags;
        private readonly Func<string> _serviceNameProvider;

        private long _lastSampledTime;
        private TimeSpan _accumulatedDuration;
        private CaptureBehaviour _captureBehaviour;
        private string _message;
        private List<EvaluationError> _errors;
        private string _snapshotId;
        private ObjectPool<MethodScopeMembers, MethodScopeMembersParameters> _scopeMembersPool;

        // Track opened JSON containers explicitly to avoid using JsonWriter.Path (allocations + heuristic parsing).
        // This class is on the hot path, so these flags should stay extremely cheap.
        private bool _debuggerOpen;
        private bool _snapshotOpen;
        private bool _capturesOpen;
        private bool _entryOpen;
        private bool _returnOpen;
        private bool _linesOpen;
        private bool _lineNumberOpen;
        private LocalsOrArgsContainer _localsOrArgsOpen;

        public DebuggerSnapshotCreator(bool isFullSnapshot, ProbeLocation location, bool hasCondition, string[] tags, CaptureLimitInfo limitInfo, bool withProcessTags, Func<string> serviceNameProvider)
        {
            _isFullSnapshot = isFullSnapshot;
            _probeLocation = location;
            _jsonUnderlyingString = StringBuilderCache.Acquire();
            JsonWriter = new JsonTextWriter(new StringWriter(_jsonUnderlyingString));
            MethodScopeMembers = default;
            _captureBehaviour = CaptureBehaviour.Capture;
            _errors = null;
            _message = null;
            ProbeHasCondition = hasCondition;
            Tags = tags;
            _limitInfo = limitInfo;
            _injectProcessTags = withProcessTags;
            _serviceNameProvider = serviceNameProvider;
            _accumulatedDuration = new TimeSpan(0, 0, 0, 0, 0);
            _scopeMembersPool = new ObjectPool<MethodScopeMembers, MethodScopeMembersParameters>();
            Initialize();
        }

        public DebuggerSnapshotCreator(bool isFullSnapshot, ProbeLocation location, bool hasCondition, string[] tags, MethodScopeMembers methodScopeMembers, CaptureLimitInfo limitInfo, bool withProcessTags, Func<string> serviceNameProvider)
            : this(isFullSnapshot, location, hasCondition, tags, limitInfo, withProcessTags, serviceNameProvider)
        {
            MethodScopeMembers = methodScopeMembers;
        }

        private enum LocalsOrArgsContainer : byte
        {
            None = 0,
            Arguments = 1,
            Locals = 2,
        }

        internal virtual string DebuggerProduct => DebuggerTags.DebuggerProduct.DI;

        internal string SnapshotId
        {
            get
            {
                _snapshotId ??= Guid.NewGuid().ToString();
                return _snapshotId;
            }
        }

        internal MethodScopeMembers MethodScopeMembers { get; private set; }

        internal bool ProbeHasCondition { get; }

        internal string[] Tags { get; }

        internal CaptureBehaviour CaptureBehaviour
        {
            get => _captureBehaviour;
            set
            {
                if (value == CaptureBehaviour.Stop)
                {
                    throw new InvalidOperationException("The value is not a valid value");
                }

                _captureBehaviour = value;
            }
        }

        internal void StartSampling()
        {
            _lastSampledTime = Stopwatch.GetTimestamp();
        }

        internal void StopSampling()
        {
            _accumulatedDuration += StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _lastSampledTime);
        }

        internal CaptureBehaviour DefineSnapshotBehavior<TCapture>(ref CaptureInfo<TCapture> info, EvaluateAt evaluateAt, bool hasCondition)
        {
            if (CaptureBehaviour == CaptureBehaviour.Stop)
            {
                // Entry evaluation evaluated to false
                return CaptureBehaviour;
            }

            if (!hasCondition)
            {
                if (_isFullSnapshot)
                {
                    // Log template with capture all - capture all values
                    CaptureBehaviour =
                        (evaluateAt == EvaluateAt.Entry && info.MethodState.IsInEntryEnd()) ||
                        (evaluateAt == EvaluateAt.Exit && info.MethodState.IsInExitEnd())
                            ? CaptureBehaviour.Evaluate
                            : CaptureBehaviour.Capture;
                }
                else
                {
                    // Log template without capture all - capture only template message
                    if ((evaluateAt == EvaluateAt.Entry && info.MethodState.IsInEntryEnd()) ||
                        (evaluateAt == EvaluateAt.Exit && info.MethodState.IsInExitEnd()))
                    {
                        CaptureBehaviour = CaptureBehaviour.Evaluate;
                    }
                    else if ((evaluateAt == EvaluateAt.Entry && info.MethodState.IsInEntry()) ||
                             (evaluateAt == EvaluateAt.Exit && info.MethodState.IsInExit()))
                    {
                        CaptureBehaviour = CaptureBehaviour.Delay;
                    }
                    else
                    {
                        CaptureBehaviour = CaptureBehaviour.NoCapture;
                    }
                }
            }
            else
            {
                if ((evaluateAt == EvaluateAt.Entry && info.MethodState.IsInEntryEnd()) ||
                    (evaluateAt == EvaluateAt.Exit && info.MethodState.IsInExitEnd()))
                {
                    // Evaluate if we are in the correct state
                    CaptureBehaviour = CaptureBehaviour.Evaluate;
                }
                else if (evaluateAt == EvaluateAt.Entry && info.MethodState.IsInExit())
                {
                    // Capture is we already evaluated to true (if we evaluated false, we exited earlier because the behaviour is "CaptureBehaviour.NoCapture")
                    CaptureBehaviour = CaptureBehaviour.Capture;
                }
                else if (evaluateAt == EvaluateAt.Exit && info.MethodState.IsInEntry())
                {
                    // Delay if we haven't in the correct state yet
                    CaptureBehaviour = CaptureBehaviour.NoCapture;
                }
                else
                {
                    CaptureBehaviour = CaptureBehaviour.Delay;
                }
            }

            if (info.MethodState.IsInStartMarkerOrBeginLine())
            {
                CreateMethodScopeMembers(ref info);
            }

            return CaptureBehaviour;
        }

        internal void Stop()
        {
            _captureBehaviour = CaptureBehaviour.Stop;
        }

        internal void CreateMethodScopeMembers<T>(ref CaptureInfo<T> info)
        {
            if (info.IsAsyncCapture())
            {
                MethodScopeMembers = _scopeMembersPool.Get(
                    new MethodScopeMembersParameters(
                        info.AsyncCaptureInfo.HoistedLocals.Length + (info.LocalsCount ?? 0),
                        info.AsyncCaptureInfo.HoistedArguments.Length + (info.ArgumentsCount ?? 0)));
            }
            else
            {
                MethodScopeMembers = _scopeMembersPool.Get(new MethodScopeMembersParameters(info.LocalsCount.Value, info.ArgumentsCount.Value));
            }
        }

        internal void AddScopeMember<T>(string name, Type type, T value, ScopeMemberKind memberKind)
        {
            if (MethodScopeMembers == null)
            {
                return;
            }

            type = (type.IsGenericTypeDefinition ? value?.GetType() : type) ?? type;
            switch (memberKind)
            {
                case ScopeMemberKind.This:
                    MethodScopeMembers.InvocationTarget = new ScopeMember(name, type, value, ScopeMemberKind.This);
                    return;
                case ScopeMemberKind.Exception:
                    MethodScopeMembers.Exception = value as Exception;
                    return;
                case ScopeMemberKind.Return:
                    MethodScopeMembers.Return = new ScopeMember("return", type, value, ScopeMemberKind.Return);
                    return;
                case ScopeMemberKind.None:
                    return;
            }

            MethodScopeMembers.AddMember(new ScopeMember(name, type, value, memberKind));
        }

        internal void SetDuration()
        {
            MethodScopeMembers.Duration = new ScopeMember("duration", typeof(double), _accumulatedDuration.TotalMilliseconds, ScopeMemberKind.Duration);
        }

        internal void Initialize()
        {
            JsonWriter.WriteStartObject();
            StartDebugger();
            StartSnapshot();
            if (_isFullSnapshot)
            {
                StartCaptures();
            }
        }

        internal void StartDebugger()
        {
            JsonWriter.WritePropertyName("debugger");
            JsonWriter.WriteStartObject();
            _debuggerOpen = true;
        }

        internal void StartSnapshot()
        {
            JsonWriter.WritePropertyName("snapshot");
            JsonWriter.WriteStartObject();
            _snapshotOpen = true;
        }

        internal void StartCaptures()
        {
            JsonWriter.WritePropertyName("captures");
            JsonWriter.WriteStartObject();
            _capturesOpen = true;
        }

        internal void StartEntry()
        {
            JsonWriter.WritePropertyName("entry");
            JsonWriter.WriteStartObject();
            _entryOpen = true;
        }

        internal void StartLines(int lineNumber)
        {
            // For non-full snapshots we still want "captures", but it might not have been started yet.
            if (!_isFullSnapshot && !_capturesOpen)
            {
                StartCaptures();
            }

            JsonWriter.WritePropertyName("lines");
            JsonWriter.WriteStartObject();
            _linesOpen = true;

            JsonWriter.WritePropertyName(lineNumber.ToString());
            JsonWriter.WriteStartObject();
            _lineNumberOpen = true;
        }

        internal void EndEntry()
        {
            // Some instrumentations intentionally skip capturing args/locals (e.g. methods with byref-like args),
            // and closing a non-open container corrupts the JsonWriter state ("No token to close").
            CloseLocalsOrArgsIfOpen();

            if (_entryOpen)
            {
                JsonWriter.WriteEndObject();
                _entryOpen = false;
            }
        }

        internal void StartReturn()
        {
            // StartCaptures is done during Initialize() for full snapshots. For non-full snapshots, we need it here,
            // but only once.
            if (!_isFullSnapshot && !_capturesOpen)
            {
                StartCaptures();
            }

            // Defensive: depending on capture behaviour and delayed snapshots, StartReturn can be called more than once.
            // Ensure we do not start nested "return".
            if (_returnOpen)
            {
                return;
            }

            // Close any open locals/arguments container, then close "entry" if it's still open.
            CloseLocalsOrArgsIfOpen();
            if (_entryOpen)
            {
                JsonWriter.WriteEndObject();
                _entryOpen = false;
            }

            JsonWriter.WritePropertyName("return");
            JsonWriter.WriteStartObject();
            _returnOpen = true;
        }

        internal void EndReturn()
        {
            // Some instrumentations intentionally skip capturing args/locals (e.g. methods with byref-like args),
            // and closing a non-open container corrupts the JsonWriter state ("No token to close").
            CloseLocalsOrArgsIfOpen();

            // Close the innermost container for this phase:
            // - method probes: close "return"
            // - line probes: close the line number object (lines.<n>)
            if (_returnOpen)
            {
                JsonWriter.WriteEndObject();
                _returnOpen = false;
            }
            else if (_lineNumberOpen)
            {
                JsonWriter.WriteEndObject();
                _lineNumberOpen = false;
            }

            if (_probeLocation == ProbeLocation.Line)
            {
                if (_linesOpen)
                {
                    JsonWriter.WriteEndObject();
                    _linesOpen = false;
                }
            }

            EndCapture();
        }

        internal void EndCapture()
        {
            if (_capturesOpen)
            {
                JsonWriter.WriteEndObject();
                _capturesOpen = false;
            }
        }

        internal DebuggerSnapshotCreator EndDebugger()
        {
            if (_debuggerOpen)
            {
                JsonWriter.WriteEndObject();
                _debuggerOpen = false;
            }

            return this;
        }

        internal virtual DebuggerSnapshotCreator EndSnapshot()
        {
            // If any capture containers are still open for any reason, close them first (do not over-close).
            CloseLocalsOrArgsIfOpen();

            if (_entryOpen)
            {
                JsonWriter.WriteEndObject();
                _entryOpen = false;
            }

            if (_returnOpen)
            {
                JsonWriter.WriteEndObject();
                _returnOpen = false;
            }

            if (_lineNumberOpen)
            {
                JsonWriter.WriteEndObject();
                _lineNumberOpen = false;
            }

            if (_linesOpen)
            {
                JsonWriter.WriteEndObject();
                _linesOpen = false;
            }

            if (_capturesOpen)
            {
                JsonWriter.WriteEndObject();
                _capturesOpen = false;
            }

            JsonWriter.WritePropertyName("id");
            JsonWriter.WriteValue(SnapshotId);

            JsonWriter.WritePropertyName("timestamp");
            JsonWriter.WriteValue(DateTimeOffset.Now.ToUnixTimeMilliseconds());

            JsonWriter.WritePropertyName("duration");
            JsonWriter.WriteValue(_accumulatedDuration.TotalMilliseconds);

            JsonWriter.WritePropertyName("language");
            JsonWriter.WriteValue(TracerConstants.Language);

            if (_snapshotOpen)
            {
                JsonWriter.WriteEndObject();
                _snapshotOpen = false;
            }

            return this;
        }

        private void CloseLocalsOrArgsIfOpen()
        {
            if (_localsOrArgsOpen != LocalsOrArgsContainer.None)
            {
                JsonWriter.WriteEndObject();
                _localsOrArgsOpen = LocalsOrArgsContainer.None;
            }
        }

        internal void CaptureInstance<TInstance>(TInstance instance, Type type)
        {
            if (instance == null)
            {
                return;
            }

            CaptureArgument(instance, "this", type);
        }

        public void CaptureStaticFields<T>(ref CaptureInfo<T> info)
        {
            if (info.IsAsyncCapture())
            {
                DebuggerSnapshotSerializer.SerializeStaticFields(info.AsyncCaptureInfo.KickoffInvocationTargetType, JsonWriter, _limitInfo);
            }
            else
            {
                DebuggerSnapshotSerializer.SerializeStaticFields(info.InvocationTargetType, JsonWriter, _limitInfo);
            }
        }

        internal void CaptureArgument<TArg>(TArg value, string name, Type type = null)
        {
            StartLocalsOrArgsIfNeeded("arguments");
            // in case TArg is object and we have the concrete type, use it
            DebuggerSnapshotSerializer.Serialize(value, type ?? typeof(TArg), name, JsonWriter, _limitInfo);
        }

        internal void CaptureLocal<TLocal>(TLocal value, string name, Type type = null)
        {
            StartLocalsOrArgsIfNeeded("locals");
            // in case TLocal is object and we have the concrete type, use it
            DebuggerSnapshotSerializer.Serialize(value, type ?? typeof(TLocal), name, JsonWriter, _limitInfo);
        }

        internal void CaptureException(Exception ex)
        {
            JsonWriter.WritePropertyName("throwable");
            JsonWriter.WriteStartObject();
            JsonWriter.WritePropertyName("message");
            JsonWriter.WriteValue(ex.Message);
            JsonWriter.WritePropertyName("type");
            JsonWriter.WriteValue(ex.GetType().FullName);
            JsonWriter.WritePropertyName("stacktrace");
            JsonWriter.WriteStartArray();
            AddFrames(new StackTrace(ex).GetFrames() ?? Array.Empty<StackFrame>());
            JsonWriter.WriteEndArray();
            JsonWriter.WriteEndObject();
        }

        internal void CaptureEntryMethodStartMarker<T>(ref CaptureInfo<T> info)
        {
            StartEntry();
            CaptureStaticFields(ref info);
        }

        internal void CaptureEntryMethodEndMarker<TTarget>(TTarget value, Type type)
        {
            CaptureInstance(value, type);
            EndEntry();
        }

        internal void CaptureExitMethodStartMarker<TReturnOrException>(ref CaptureInfo<TReturnOrException> info)
        {
            StartReturn();
            switch (info.MethodState)
            {
                case MethodState.ExitStart:
                case MethodState.ExitEnd:
                    ExitMethodStart(ref info);
                    break;
                case MethodState.ExitStartAsync:
                case MethodState.ExitEndAsync:
                    ExitAsyncMethodStart(ref info);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ExitMethodStart<TReturnOrException>(ref CaptureInfo<TReturnOrException> info)
        {
            CaptureStaticFields(ref info);

            switch (info.MethodState)
            {
                case MethodState.ExitStartAsync:
                case MethodState.ExitStart:
                    if (info.MemberKind == ScopeMemberKind.Exception && info.Value != null)
                    {
                        CaptureException(info.Value as Exception);
                        CaptureLocal(info.Value, "@exception", info.Type);
                    }
                    else if (info.MemberKind == ScopeMemberKind.Return)
                    {
                        CaptureLocal(info.Value, "@return", info.Type);
                    }

                    break;
                case MethodState.ExitEndAsync:
                case MethodState.ExitEnd:
                    if (MethodScopeMembers.Exception != null)
                    {
                        CaptureException(MethodScopeMembers.Exception);
                        CaptureLocal(MethodScopeMembers.Exception, "@exception", MethodScopeMembers.Exception.GetType());
                    }
                    else if (MethodScopeMembers.Return.Type != null)
                    {
                        CaptureLocal(MethodScopeMembers.Return.Value, "@return", MethodScopeMembers.Return.Type);
                    }

                    break;
                default:
                    break;
            }
        }

        internal void CaptureExitMethodEndMarker<TTarget>(ref CaptureInfo<TTarget> info)
        {
            CaptureInstance(info.Value, info.Type);
            if (info.MethodState == MethodState.ExitEndAsync)
            {
                CaptureAsyncMethodArguments(info.AsyncCaptureInfo.HoistedArguments, info.AsyncCaptureInfo.MoveNextInvocationTarget);
            }

            EndReturn();
        }

        internal void CaptureEntryAsyncMethod<T>(ref CaptureInfo<T> info)
        {
            CaptureEntryMethodStartMarker(ref info);
            CaptureAsyncMethodArguments(info.AsyncCaptureInfo.HoistedArguments, info.AsyncCaptureInfo.MoveNextInvocationTarget);
            CaptureEntryMethodEndMarker(info.Value, info.Type);
        }

        internal void SetEvaluationResult(ref ExpressionEvaluationResult evaluationResult)
        {
            _message = evaluationResult.Template;
            _errors = evaluationResult.Errors;
        }

        private void CaptureAsyncMethodArguments(System.Reflection.FieldInfo[] asyncHoistedArguments, object moveNextInvocationTarget)
        {
            // capture hoisted arguments
            for (var index = 0; index < asyncHoistedArguments.Length; index++)
            {
                ref var argument = ref asyncHoistedArguments[index];
                if (argument == default)
                {
                    continue;
                }

                var argumentValue = argument.GetValue(moveNextInvocationTarget);
                CaptureArgument(argumentValue, argument.Name, argumentValue?.GetType() ?? argument.FieldType);
            }
        }

        private void ExitAsyncMethodStart<T>(ref CaptureInfo<T> info)
        {
            ExitMethodStart(ref info);
            CaptureAsyncMethodLocals(info.AsyncCaptureInfo.HoistedLocals, info.AsyncCaptureInfo.MoveNextInvocationTarget);
        }

        private void CaptureAsyncMethodLocals(AsyncHelper.FieldInfoNameSanitized[] hoistedLocals, object moveNextInvocationTarget)
        {
            // MethodMetadataInfo saves locals from MoveNext localVarSig,
            // this isn't enough in async scenario because we need to extract more locals the may hoisted in the builder object
            // and we need to subtract some locals that exist in the localVarSig but they are not belongs to the kickoff method
            // For know we capturing here all locals the are hoisted (except known generated locals)
            // and we capturing in LogLocal the locals form localVarSig
            for (var index = 0; index < hoistedLocals.Length; index++)
            {
                ref var local = ref hoistedLocals[index];
                if (local == default)
                {
                    continue;
                }

                var localValue = local.Field.GetValue(moveNextInvocationTarget);
                CaptureLocal(localValue, local.SanitizedName, localValue?.GetType() ?? local.Field.FieldType);
            }
        }

        internal void CaptureBeginLine<T>(ref CaptureInfo<T> info)
        {
            StartLines(info.LineCaptureInfo.LineNumber);
            CaptureStaticFields(ref info);
        }

        internal void CaptureEndLine<TTarget>(ref CaptureInfo<TTarget> info)
        {
            switch (info.MethodState)
            {
                case MethodState.EndLine:
                    EndLine(ref info);
                    break;
                case MethodState.EndLineAsync:
                    EndAsyncLine(ref info);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void EndLine<TTarget>(ref CaptureInfo<TTarget> info)
        {
            CaptureInstance(info.Value, info.Type);
            EndReturn();
        }

        private void EndAsyncLine<TTarget>(ref CaptureInfo<TTarget> info)
        {
            CaptureAsyncMethodLocals(info.AsyncCaptureInfo.HoistedLocals, info.AsyncCaptureInfo.MoveNextInvocationTarget);
            CaptureInstance(info.AsyncCaptureInfo.KickoffInvocationTarget, info.AsyncCaptureInfo.KickoffInvocationTargetType);
            CaptureAsyncMethodArguments(info.AsyncCaptureInfo.HoistedArguments, info.AsyncCaptureInfo.MoveNextInvocationTarget);
            EndReturn();
        }

        internal bool ProcessDelayedSnapshot<TCapture>(ref CaptureInfo<TCapture> captureInfo, bool hasCondition)
        {
            if (CaptureBehaviour == CaptureBehaviour.Evaluate && (hasCondition || !_isFullSnapshot))
            {
                switch (captureInfo.MethodState)
                {
                    case MethodState.EntryEnd:
                        CaptureEntryMethodStartMarker(ref captureInfo);
                        break;
                    case MethodState.EntryAsync:
                        CaptureEntryAsyncMethod(ref captureInfo);
                        return true;
                    case MethodState.ExitEnd:
                        CaptureExitMethodStartMarker(ref captureInfo);
                        break;
                    case MethodState.ExitEndAsync:
                        CaptureExitMethodStartMarker(ref captureInfo);
                        CaptureScopeMembers(MethodScopeMembers.Members, ScopeMemberKind.Local);
                        return true;
                    case MethodState.EndLine:
                    case MethodState.EndLineAsync:
                        CaptureBeginLine(ref captureInfo);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(captureInfo.MethodState), captureInfo.MethodState, null);
                }

                CaptureScopeMembers(MethodScopeMembers.Members);
                return true;
            }

            return false;
        }

        internal void CaptureScopeMembers(ScopeMember[] members, ScopeMemberKind? kind = null)
        {
            foreach (var member in members)
            {
                if (member.Type == null)
                {
                    // ArrayPool can allocate more items than we need, if "Type == null", this mean we can exit the loop because Type should never be null
                    break;
                }

                if (kind != null && kind.Value != member.ElementType)
                {
                    continue;
                }

                switch (member.ElementType)
                {
                    case ScopeMemberKind.Argument:
                        {
                            CaptureArgument(member.Value, member.Name, member.Type);
                            break;
                        }

                    case ScopeMemberKind.Local:
                    case ScopeMemberKind.Return:
                        {
                            CaptureLocal(member.Value, member.Name, member.Type);
                            break;
                        }

                    case ScopeMemberKind.Exception:
                        {
                            CaptureException((Exception)member.Value);
                            CaptureLocal((Exception)member.Value, member.Name, member.Type);
                            break;
                        }
                }
            }
        }

        private void StartLocalsOrArgsIfNeeded(string newParent)
        {
            var desired =
                newParent == "arguments"
                    ? LocalsOrArgsContainer.Arguments
                    : LocalsOrArgsContainer.Locals;

            if (_localsOrArgsOpen == desired)
            {
                // We're already there!
                return;
            }

            // "locals" should always come after "arguments"
            CloseLocalsOrArgsIfOpen();

            JsonWriter.WritePropertyName(newParent);
            JsonWriter.WriteStartObject();
            _localsOrArgsOpen = desired;
        }

        // Finalize snapshot
        internal string FinalizeLineSnapshot<T>(string probeId, int probeVersion, ref CaptureInfo<T> info)
        {
            using (this)
            {
                var methodName = info.MethodState == MethodState.EndLineAsync
                                     ? info.AsyncCaptureInfo.KickoffMethod?.Name
                                     : info.Method?.Name;

                var typeFullName = info.MethodState == MethodState.EndLineAsync
                                       ? info.AsyncCaptureInfo.KickoffInvocationTargetType?.FullName
                                       : info.InvocationTargetType?.FullName;

                AddEvaluationErrors()
                   .AddProbeInfo(
                        probeId,
                        probeVersion,
                        info.LineCaptureInfo.LineNumber,
                        info.LineCaptureInfo.ProbeFilePath)
                   .FinalizeSnapshot(
                        methodName,
                        typeFullName,
                        info.LineCaptureInfo.ProbeFilePath);

                var snapshot = GetSnapshotJson();
                return snapshot;
            }
        }

        internal string FinalizeMethodSnapshot<T>(string probeId, int probeVersion, ref CaptureInfo<T> info)
        {
            using (this)
            {
                var methodName = info.MethodState == MethodState.ExitEndAsync
                                     ? info.AsyncCaptureInfo.KickoffMethod?.Name
                                     : info.Method?.Name;

                var typeFullName = info.MethodState == MethodState.ExitEndAsync
                                       ? info.AsyncCaptureInfo.KickoffInvocationTargetType?.FullName
                                       : info.InvocationTargetType?.FullName;
                AddEvaluationErrors()
                   .AddProbeInfo(
                        probeId,
                        probeVersion,
                        methodName,
                        typeFullName)
                   .FinalizeSnapshot(
                        methodName,
                        typeFullName,
                        null);

                var snapshot = GetSnapshotJson();
                return snapshot;
            }
        }

        internal void FinalizeSnapshot(string methodName, string typeFullName, string probeFilePath)
        {
            var activeScope = Tracer.Instance.InternalActiveScope;

            // TODO: support 128-bit trace ids?
            var traceId = activeScope?.Span.TraceId128.Lower.ToString(CultureInfo.InvariantCulture);
            var spanId = activeScope?.Span.SpanId.ToString(CultureInfo.InvariantCulture);

            AddStackInfo()
            .EndSnapshot()
            .EndDebugger()
            .AddLoggerInfo(methodName, typeFullName, probeFilePath)
            .AddGeneralInfo(_serviceNameProvider(), ProcessTags.SerializedTags, traceId, spanId)
            .AddMessage()
            .Complete();
        }

        internal DebuggerSnapshotCreator AddEvaluationErrors()
        {
            if (_errors == null || _errors.Count == 0)
            {
                return this;
            }

            JsonWriter.WritePropertyName("evaluationErrors");
            JsonWriter.WriteStartArray();
            foreach (var error in _errors)
            {
                JsonWriter.WriteStartObject();
                JsonWriter.WritePropertyName("expr");
                JsonWriter.WriteValue(error.Expression);
                JsonWriter.WritePropertyName("message");
                JsonWriter.WriteValue(error.Message);
                JsonWriter.WriteEndObject();
            }

            JsonWriter.WriteEndArray();
            return this;
        }

        internal DebuggerSnapshotCreator AddProbeInfo<T>(string probeId, int probeVersion, T methodNameOrLineNumber, string typeFullNameOrFilePath)
        {
            JsonWriter.WritePropertyName("probe");
            JsonWriter.WriteStartObject();

            JsonWriter.WritePropertyName("id");
            JsonWriter.WriteValue(probeId);

            JsonWriter.WritePropertyName("version");
            JsonWriter.WriteValue(probeVersion);

            JsonWriter.WritePropertyName("location");
            JsonWriter.WriteStartObject();

            if (_probeLocation == ProbeLocation.Method)
            {
                JsonWriter.WritePropertyName("method");
                JsonWriter.WriteValue(methodNameOrLineNumber);

                JsonWriter.WritePropertyName("type");
                JsonWriter.WriteValue(typeFullNameOrFilePath ?? UnknownValue);
            }
            else
            {
                JsonWriter.WritePropertyName("file");
                JsonWriter.WriteValue(SanitizePath(typeFullNameOrFilePath));

                JsonWriter.WritePropertyName("lines");
                JsonWriter.WriteStartArray();
                JsonWriter.WriteValue(methodNameOrLineNumber.ToString());
                JsonWriter.WriteEndArray();
            }

            JsonWriter.WriteEndObject();
            JsonWriter.WriteEndObject();

            return this;
        }

        private static string SanitizePath(string probeFilePath)
        {
            return string.IsNullOrEmpty(probeFilePath) ? null : probeFilePath.Replace('\\', '/');
        }

        private DebuggerSnapshotCreator AddStackInfo()
        {
            if (!_isFullSnapshot)
            {
                return this;
            }

            var stackFrames = (new StackTrace(true).GetFrames() ?? Array.Empty<StackFrame>())
                             .SkipWhile(frame => frame?.GetMethod()?.DeclaringType?.Namespace?.StartsWith("Datadog") == true).ToArray();

            JsonWriter.WritePropertyName("stack");
            JsonWriter.WriteStartArray();
            AddFrames(stackFrames);
            JsonWriter.WriteEndArray();

            return this;
        }

        private void AddFrames(StackFrame[] frames)
        {
            foreach (var frame in frames)
            {
                JsonWriter.WriteStartObject();
                JsonWriter.WritePropertyName("function");
                var frameMethod = frame.GetMethod();
                JsonWriter.WriteValue($"{frameMethod?.DeclaringType?.FullName ?? UnknownValue}.{frameMethod?.Name ?? UnknownValue}");

                var fileName = frame.GetFileName();
                if (fileName != null)
                {
                    JsonWriter.WritePropertyName("fileName");
                    JsonWriter.WriteValue(frame.GetFileName());
                }

                JsonWriter.WritePropertyName("lineNumber");
                JsonWriter.WriteValue(frame.GetFileLineNumber());
                JsonWriter.WriteEndObject();
            }
        }

        internal DebuggerSnapshotCreator AddLoggerInfo(string methodName, string typeFullName, string probeFilePath)
        {
            JsonWriter.WritePropertyName("logger");
            JsonWriter.WriteStartObject();

            var thread = Thread.CurrentThread;
            JsonWriter.WritePropertyName("thread_id");
            JsonWriter.WriteValue(thread.ManagedThreadId);

            JsonWriter.WritePropertyName("thread_name");
            JsonWriter.WriteValue(thread.Name);

            JsonWriter.WritePropertyName("version");
            JsonWriter.WriteValue(LoggerVersion);

            JsonWriter.WritePropertyName("name");
            JsonWriter.WriteValue(typeFullName ?? SanitizePath(probeFilePath));

            JsonWriter.WritePropertyName("method");
            JsonWriter.WriteValue(methodName);

            JsonWriter.WriteEndObject();

            return this;
        }

        internal DebuggerSnapshotCreator AddGeneralInfo(string service, string processTags, string traceId, string spanId)
        {
            JsonWriter.WritePropertyName("service");
            JsonWriter.WriteValue(service ?? UnknownValue);

            if (_injectProcessTags && !string.IsNullOrEmpty(processTags))
            {
                JsonWriter.WritePropertyName("process_tags");
                JsonWriter.WriteValue(processTags);
            }

            JsonWriter.WritePropertyName("ddsource");
            JsonWriter.WriteValue(DebuggerTags.DDSource);

            JsonWriter.WritePropertyName("dd.trace_id");
            JsonWriter.WriteValue(traceId);

            JsonWriter.WritePropertyName("dd.span_id");
            JsonWriter.WriteValue(spanId);

            JsonWriter.WritePropertyName("debugger.type");
            JsonWriter.WriteValue(DebuggerTags.DebuggerType.Snapshot);

            JsonWriter.WritePropertyName("debugger.product");
            JsonWriter.WriteValue(DebuggerProduct);

            return this;
        }

        public DebuggerSnapshotCreator AddMessage()
        {
            JsonWriter.WritePropertyName("message");
            JsonWriter.WriteValue(_message);
            return this;
        }

        public DebuggerSnapshotCreator Complete()
        {
            // Finalization must be resilient: depending on the capture flow (delayed snapshots, skipped sections, etc.)
            // we may already be at the root/start state. In that case, attempting to close again throws JsonWriterException.
            // We still need to ensure the payload is a complete JSON document before GetSnapshotJson() is called
            try
            {
                if (JsonWriter is null)
                {
                    return this;
                }

                // Close *all* open containers, not just the innermost one.
                // WriteState only reflects the current writer position; if we're inside an array, closing it once
                // can still leave us inside one or more outer objects.
                // Cap the number of iterations.
                for (var i = 0; i < 32; i++)
                {
                    switch (JsonWriter.WriteState)
                    {
                        case WriteState.Start:
                        case WriteState.Closed:
                            // Nothing open to close (already completed)
                            return this;

                        case WriteState.Property:
                            // If we ended up mid-property, write a null value to complete it and continue closing.
                            JsonWriter.WriteNull();
                            break;

                        case WriteState.Object:
                            JsonWriter.WriteEndObject();
                            break;

                        case WriteState.Array:
                            JsonWriter.WriteEndArray();
                            break;

                        default:
                            // Fallback to Close(), which will attempt to auto-complete the JSON.
                            JsonWriter.Close();
                            return this;
                    }
                }

                // Best-effort: if we still haven't reached Start/Closed, let Json.NET try to auto-complete.
                JsonWriter.Close();
                return this;
            }
            catch (JsonWriterException)
            {
                // Best-effort: never fail probe processing due to a close mismatch.
                return this;
            }
        }

        internal string GetSnapshotJson()
        {
            return StringBuilderCache.GetStringAndRelease(_jsonUnderlyingString);
        }

        public void Dispose()
        {
            try
            {
                Stop();
                _scopeMembersPool.Return(MethodScopeMembers);
                JsonWriter?.Close();
            }
            catch
            {
                // ignored
            }
        }
    }
}
