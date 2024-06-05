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
using Datadog.Trace.ClrProfiler;
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
        private const string DDSource = "dd_debugger";
        private const string UnknownValue = "Unknown";

        private readonly JsonTextWriter _jsonWriter;
        private readonly StringBuilder _jsonUnderlyingString;
        private readonly bool _isFullSnapshot;
        private readonly ProbeLocation _probeLocation;
        private readonly CaptureLimitInfo _limitInfo;

        private long _lastSampledTime;
        private TimeSpan _accumulatedDuration;
        private CaptureBehaviour _captureBehaviour;
        private string _message;
        private List<EvaluationError> _errors;
        private string _snapshotId;

        public DebuggerSnapshotCreator(bool isFullSnapshot, ProbeLocation location, bool hasCondition, string[] tags, CaptureLimitInfo limitInfo)
        {
            _isFullSnapshot = isFullSnapshot;
            _probeLocation = location;
            _jsonUnderlyingString = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            _jsonWriter = new JsonTextWriter(new StringWriter(_jsonUnderlyingString));
            MethodScopeMembers = default;
            _captureBehaviour = CaptureBehaviour.Capture;
            _errors = null;
            _message = null;
            ProbeHasCondition = hasCondition;
            Tags = tags;
            _limitInfo = limitInfo;
            _accumulatedDuration = new TimeSpan(0, 0, 0, 0, 0);
            Initialize();
        }

        public DebuggerSnapshotCreator(bool isFullSnapshot, ProbeLocation location, bool hasCondition, string[] tags, MethodScopeMembers methodScopeMembers, CaptureLimitInfo limitInfo)
            : this(isFullSnapshot, location, hasCondition, tags, limitInfo)
        {
            MethodScopeMembers = methodScopeMembers;
        }

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
                MethodScopeMembers = new MethodScopeMembers(info.AsyncCaptureInfo.HoistedLocals.Length + (info.LocalsCount ?? 0), info.AsyncCaptureInfo.HoistedArguments.Length + (info.ArgumentsCount ?? 0));
            }
            else
            {
                MethodScopeMembers = new MethodScopeMembers(info.LocalsCount.Value, info.ArgumentsCount.Value);
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
            _jsonWriter.WriteStartObject();
            StartDebugger();
            StartSnapshot();
            if (_isFullSnapshot)
            {
                StartCaptures();
            }
        }

        internal void StartDebugger()
        {
            _jsonWriter.WritePropertyName("debugger");
            _jsonWriter.WriteStartObject();
        }

        internal void StartSnapshot()
        {
            _jsonWriter.WritePropertyName("snapshot");
            _jsonWriter.WriteStartObject();
        }

        internal void StartCaptures()
        {
            _jsonWriter.WritePropertyName("captures");
            _jsonWriter.WriteStartObject();
        }

        internal void StartEntry()
        {
            _jsonWriter.WritePropertyName("entry");
            _jsonWriter.WriteStartObject();
        }

        internal void StartLines(int lineNumber)
        {
            _jsonWriter.WritePropertyName("lines");
            _jsonWriter.WriteStartObject();

            _jsonWriter.WritePropertyName(lineNumber.ToString());
            _jsonWriter.WriteStartObject();
        }

        internal void EndEntry(bool hasArgumentsOrLocals)
        {
            if (hasArgumentsOrLocals)
            {
                // end arguments or locals
                _jsonWriter.WriteEndObject();
            }

            // end entry
            _jsonWriter.WriteEndObject();
        }

        internal void StartReturn()
        {
            if (!_isFullSnapshot)
            {
                StartCaptures();
            }

            _jsonWriter.WritePropertyName("return");
            _jsonWriter.WriteStartObject();
        }

        internal void EndReturn(bool hasArgumentsOrLocals)
        {
            if (hasArgumentsOrLocals)
            {
                // end arguments or locals
                _jsonWriter.WriteEndObject();
            }

            // end line number or method return
            _jsonWriter.WriteEndObject();
            if (_probeLocation == ProbeLocation.Line)
            {
                // end lines
                _jsonWriter.WriteEndObject();
            }

            // end captures
            EndCapture();
        }

        internal void EndCapture()
        {
            _jsonWriter.WriteEndObject();
        }

        internal DebuggerSnapshotCreator EndDebugger()
        {
            _jsonWriter.WriteEndObject();
            return this;
        }

        internal DebuggerSnapshotCreator EndSnapshot()
        {
            _jsonWriter.WritePropertyName("id");
            _jsonWriter.WriteValue(SnapshotId);

            _jsonWriter.WritePropertyName("timestamp");
            _jsonWriter.WriteValue(DateTimeOffset.Now.ToUnixTimeMilliseconds());

            _jsonWriter.WritePropertyName("duration");
            _jsonWriter.WriteValue(_accumulatedDuration.TotalMilliseconds);

            _jsonWriter.WritePropertyName("language");
            _jsonWriter.WriteValue(TracerConstants.Language);

            _jsonWriter.WriteEndObject();
            return this;
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
                DebuggerSnapshotSerializer.SerializeStaticFields(info.AsyncCaptureInfo.KickoffInvocationTargetType, _jsonWriter, _limitInfo);
            }
            else
            {
                DebuggerSnapshotSerializer.SerializeStaticFields(info.InvocationTargetType, _jsonWriter, _limitInfo);
            }
        }

        internal void CaptureArgument<TArg>(TArg value, string name, Type type = null)
        {
            StartLocalsOrArgsIfNeeded("arguments");
            // in case TArg is object and we have the concrete type, use it
            DebuggerSnapshotSerializer.Serialize(value, type ?? typeof(TArg), name, _jsonWriter, _limitInfo);
        }

        internal void CaptureLocal<TLocal>(TLocal value, string name, Type type = null)
        {
            StartLocalsOrArgsIfNeeded("locals");
            // in case TLocal is object and we have the concrete type, use it
            DebuggerSnapshotSerializer.Serialize(value, type ?? typeof(TLocal), name, _jsonWriter, _limitInfo);
        }

        internal void CaptureException(Exception ex)
        {
            _jsonWriter.WritePropertyName("throwable");
            _jsonWriter.WriteStartObject();
            _jsonWriter.WritePropertyName("message");
            _jsonWriter.WriteValue(ex.Message);
            _jsonWriter.WritePropertyName("type");
            _jsonWriter.WriteValue(ex.GetType().FullName);
            _jsonWriter.WritePropertyName("stacktrace");
            _jsonWriter.WriteStartArray();
            AddFrames(new StackTrace(ex).GetFrames() ?? Array.Empty<StackFrame>());
            _jsonWriter.WriteEndArray();
            _jsonWriter.WriteEndObject();
        }

        internal void CaptureEntryMethodStartMarker<T>(ref CaptureInfo<T> info)
        {
            StartEntry();
            CaptureStaticFields(ref info);
        }

        internal void CaptureEntryMethodEndMarker<TTarget>(TTarget value, Type type, bool hasArgumentsOrLocal)
        {
            CaptureInstance(value, type);
            EndEntry(hasArgumentsOrLocal || value != null);
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

            EndReturn(info.HasLocalOrArgument.Value);
        }

        internal void CaptureEntryAsyncMethod<T>(ref CaptureInfo<T> info)
        {
            CaptureEntryMethodStartMarker(ref info);
            bool hasArgument = CaptureAsyncMethodArguments(info.AsyncCaptureInfo.HoistedArguments, info.AsyncCaptureInfo.MoveNextInvocationTarget);
            CaptureEntryMethodEndMarker(info.Value, info.Type, hasArgument);
        }

        internal void SetEvaluationResult(ref ExpressionEvaluationResult evaluationResult)
        {
            _message = evaluationResult.Template;
            _errors = evaluationResult.Errors;
        }

        private bool CaptureAsyncMethodArguments(System.Reflection.FieldInfo[] asyncHoistedArguments, object moveNextInvocationTarget)
        {
            // capture hoisted arguments
            var hasArgument = false;
            for (var index = 0; index < asyncHoistedArguments.Length; index++)
            {
                ref var argument = ref asyncHoistedArguments[index];
                if (argument == default)
                {
                    continue;
                }

                var argumentValue = argument.GetValue(moveNextInvocationTarget);
                CaptureArgument(argumentValue, argument.Name, argumentValue?.GetType() ?? argument.FieldType);
                hasArgument = true;
            }

            return hasArgument;
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
            EndReturn(info.HasLocalOrArgument.Value);
        }

        private void EndAsyncLine<TTarget>(ref CaptureInfo<TTarget> info)
        {
            CaptureAsyncMethodLocals(info.AsyncCaptureInfo.HoistedLocals, info.AsyncCaptureInfo.MoveNextInvocationTarget);
            CaptureInstance(info.AsyncCaptureInfo.KickoffInvocationTarget, info.AsyncCaptureInfo.KickoffInvocationTargetType);
            CaptureAsyncMethodArguments(info.AsyncCaptureInfo.HoistedArguments, info.AsyncCaptureInfo.MoveNextInvocationTarget);
            EndReturn(info.HasLocalOrArgument.Value);
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
            var currentParent = _jsonWriter.Path.Split('.').LastOrDefault(p => p is "locals" or "arguments");
            if (currentParent == newParent)
            {
                // We're already there!
                return;
            }

            // "locals" should always come after "arguments"
            if ((currentParent == "locals" && newParent == "arguments") ||
                (currentParent == "arguments" && newParent == "locals"))
            {
                // We need to close the previous node first.
                _jsonWriter.WriteEndObject();
            }

            _jsonWriter.WritePropertyName(newParent);
            _jsonWriter.WriteStartObject();
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
            .AddGeneralInfo(DynamicInstrumentationHelper.ServiceName, traceId, spanId)
            .AddMessage()
            .Complete();
        }

        internal DebuggerSnapshotCreator AddEvaluationErrors()
        {
            if (_errors == null || _errors.Count == 0)
            {
                return this;
            }

            _jsonWriter.WritePropertyName("evaluationErrors");
            _jsonWriter.WriteStartArray();
            foreach (var error in _errors)
            {
                _jsonWriter.WriteStartObject();
                _jsonWriter.WritePropertyName("expr");
                _jsonWriter.WriteValue(error.Expression);
                _jsonWriter.WritePropertyName("message");
                _jsonWriter.WriteValue(error.Message);
                _jsonWriter.WriteEndObject();
            }

            _jsonWriter.WriteEndArray();
            return this;
        }

        internal DebuggerSnapshotCreator AddProbeInfo<T>(string probeId, int probeVersion, T methodNameOrLineNumber, string typeFullNameOrFilePath)
        {
            _jsonWriter.WritePropertyName("probe");
            _jsonWriter.WriteStartObject();

            _jsonWriter.WritePropertyName("id");
            _jsonWriter.WriteValue(probeId);

            _jsonWriter.WritePropertyName("version");
            _jsonWriter.WriteValue(probeVersion);

            _jsonWriter.WritePropertyName("location");
            _jsonWriter.WriteStartObject();

            if (_probeLocation == ProbeLocation.Method)
            {
                _jsonWriter.WritePropertyName("method");
                _jsonWriter.WriteValue(methodNameOrLineNumber);

                _jsonWriter.WritePropertyName("type");
                _jsonWriter.WriteValue(typeFullNameOrFilePath ?? UnknownValue);
            }
            else
            {
                _jsonWriter.WritePropertyName("file");
                _jsonWriter.WriteValue(SanitizePath(typeFullNameOrFilePath));

                _jsonWriter.WritePropertyName("lines");
                _jsonWriter.WriteStartArray();
                _jsonWriter.WriteValue(methodNameOrLineNumber.ToString());
                _jsonWriter.WriteEndArray();
            }

            _jsonWriter.WriteEndObject();
            _jsonWriter.WriteEndObject();

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

            _jsonWriter.WritePropertyName("stack");
            _jsonWriter.WriteStartArray();
            AddFrames(stackFrames);
            _jsonWriter.WriteEndArray();

            return this;
        }

        private void AddFrames(StackFrame[] frames)
        {
            foreach (var frame in frames)
            {
                _jsonWriter.WriteStartObject();
                _jsonWriter.WritePropertyName("function");
                var frameMethod = frame.GetMethod();
                _jsonWriter.WriteValue($"{frameMethod?.DeclaringType?.FullName ?? UnknownValue}.{frameMethod?.Name ?? UnknownValue}");

                var fileName = frame.GetFileName();
                if (fileName != null)
                {
                    _jsonWriter.WritePropertyName("fileName");
                    _jsonWriter.WriteValue(frame.GetFileName());
                }

                _jsonWriter.WritePropertyName("lineNumber");
                _jsonWriter.WriteValue(frame.GetFileLineNumber());
                _jsonWriter.WriteEndObject();
            }
        }

        internal DebuggerSnapshotCreator AddLoggerInfo(string methodName, string typeFullName, string probeFilePath)
        {
            _jsonWriter.WritePropertyName("logger");
            _jsonWriter.WriteStartObject();

            var thread = Thread.CurrentThread;
            _jsonWriter.WritePropertyName("thread_id");
            _jsonWriter.WriteValue(thread.ManagedThreadId);

            _jsonWriter.WritePropertyName("thread_name");
            _jsonWriter.WriteValue(thread.Name);

            _jsonWriter.WritePropertyName("version");
            _jsonWriter.WriteValue(LoggerVersion);

            _jsonWriter.WritePropertyName("name");
            _jsonWriter.WriteValue(typeFullName ?? SanitizePath(probeFilePath));

            _jsonWriter.WritePropertyName("method");
            _jsonWriter.WriteValue(methodName);

            _jsonWriter.WriteEndObject();

            return this;
        }

        internal DebuggerSnapshotCreator AddGeneralInfo(string service, string traceId, string spanId)
        {
            _jsonWriter.WritePropertyName("service");
            _jsonWriter.WriteValue(service ?? UnknownValue);

            _jsonWriter.WritePropertyName("ddsource");
            _jsonWriter.WriteValue(DDSource);

            _jsonWriter.WritePropertyName("dd.trace_id");
            _jsonWriter.WriteValue(traceId);

            _jsonWriter.WritePropertyName("dd.span_id");
            _jsonWriter.WriteValue(spanId);

            return this;
        }

        public DebuggerSnapshotCreator AddMessage()
        {
            _jsonWriter.WritePropertyName("message");
            _jsonWriter.WriteValue(_message);
            return this;
        }

        public DebuggerSnapshotCreator Complete()
        {
            _jsonWriter.WriteEndObject();
            return this;
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
                MethodScopeMembers?.Dispose();
                MethodScopeMembers = null;
                _jsonWriter?.Close();
            }
            catch
            {
                // ignored
            }
        }
    }
}
