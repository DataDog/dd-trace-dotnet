// <copyright file="DebuggerSnapshotCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    internal class DebuggerSnapshotCreator : IDisposable
    {
        private const string LoggerVersion = "2";
        private const string DDSource = "dd_debugger";
        private const string UnknownValue = "Unknown";

        private readonly JsonTextWriter _jsonWriter;
        private readonly StringBuilder _jsonUnderlyingString;
        private readonly bool _isFullSnapshot;
        private readonly ProbeLocation _probeLocation;
        private readonly DateTimeOffset? _startTime;
        private MethodScopeMembers _methodScopeMembers;

        public DebuggerSnapshotCreator(string probeId)
        {
            try
            {
                var probeInfo = ProbeExpressionsProcessor.Instance.GetProbeInfo(probeId);
                if (probeInfo.HasValue)
                {
                    _isFullSnapshot = probeInfo.Value.IsFullSnapshot;
                    _probeLocation = probeInfo.Value.ProbeLocation;
                    _jsonUnderlyingString = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                    _jsonWriter = new JsonTextWriter(new StringWriter(_jsonUnderlyingString));
                    _methodScopeMembers = null;
                    CaptureBehaviour = CaptureBehaviour.Capture;
                    _startTime = DateTimeOffset.UtcNow;
                    IsInitialized = false;
                    Initialize();
                    return;
                }
            }
            catch
            {
                // ignored
            }

            _jsonWriter = null;
            _jsonUnderlyingString = null;
            _isFullSnapshot = false;
            _probeLocation = ProbeLocation.Method;
            _methodScopeMembers = null;
            _startTime = null;
            IsInitialized = false;
            CaptureBehaviour = CaptureBehaviour.NoCapture;
        }

        internal bool IsInitialized { get; private set; }

        internal MethodScopeMembers MethodScopeMembers => _methodScopeMembers;

        internal CaptureBehaviour CaptureBehaviour { get; set; }

        internal CaptureBehaviour DefineSnapshotBehavior<TCapture>(CaptureInfo<TCapture> info, EvaluateAt evaluateAt, bool hasCondition)
        {
            if (hasCondition &&
                evaluateAt == EvaluateAt.Exit &&
                info.MethodState is MethodState.BeginLine or MethodState.EntryStart)
            {
                // in case there is a condition in exit but we are in the entry, don't save entry scope members
                CaptureBehaviour = CaptureBehaviour.NoCapture;
                return CaptureBehaviour;
            }

            if (!hasCondition && _isFullSnapshot)
            {
                CaptureBehaviour = CaptureBehaviour.Capture;
            }
            else
            {
                CaptureBehaviour = CaptureBehaviour.Delayed;
            }

            if (info.MethodState.IsInAsyncMethod())
            {
                CreateMethodScopeMembers(info.AsyncCaptureInfo.HoistedLocals.Length, info.AsyncCaptureInfo.HoistedArguments.Length);
            }
            else
            {
                CreateMethodScopeMembers(info.LocalsCount.Value, info.ArgumentsCount.Value);
            }

            return CaptureBehaviour;
        }

        internal void CreateMethodScopeMembers(int numberOfLocals, int numberOfArguments)
        {
            Interlocked.CompareExchange(ref _methodScopeMembers, new MethodScopeMembers(numberOfLocals, numberOfArguments), null);
        }

        internal void AddScopeMember<T>(string name, Type type, T value, ScopeMemberKind memberKind)
        {
            switch (memberKind)
            {
                case ScopeMemberKind.This:
                    MethodScopeMembers.InvocationTarget = new ScopeMember(name, type, value, ScopeMemberKind.This);
                    return;
                case ScopeMemberKind.Exception:
                    MethodScopeMembers.Exception = value as Exception;
                    break;
                case ScopeMemberKind.Return:
                    MethodScopeMembers.Return = new ScopeMember("return", type, value, ScopeMemberKind.Return);
                    break;
                case ScopeMemberKind.None:
                    return;
            }

            MethodScopeMembers.AddMember(new ScopeMember(name, type, value, memberKind));
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

            IsInitialized = true;
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

        internal DebuggerSnapshotCreator EndSnapshot(DateTimeOffset? startTime)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _jsonWriter.WritePropertyName("id");
            _jsonWriter.WriteValue(Guid.NewGuid());

            _jsonWriter.WritePropertyName("timestamp");
            _jsonWriter.WriteValue(DateTimeOffset.Now.ToUnixTimeMilliseconds());

            _jsonWriter.WritePropertyName("duration");
            _jsonWriter.WriteValue(duration.HasValue ? duration.Value.TotalMilliseconds : UnknownValue);

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
            if (info.MethodState.IsInAsyncMethod())
            {
                DebuggerSnapshotSerializer.SerializeStaticFields(info.AsyncCaptureInfo.KickoffInvocationTargetType, _jsonWriter);
            }
            else
            {
                DebuggerSnapshotSerializer.SerializeStaticFields(info.InvocationTargetType, _jsonWriter);
            }
        }

        internal void CaptureArgument<TArg>(TArg value, string name, Type type = null)
        {
            StartLocalsOrArgsIfNeeded("arguments");
            // in case TArg is object and we have the concrete type, use it
            DebuggerSnapshotSerializer.Serialize(value, type ?? typeof(TArg), name, _jsonWriter);
        }

        internal void CaptureLocal<TLocal>(TLocal value, string name, Type type = null)
        {
            StartLocalsOrArgsIfNeeded("locals");
            // in case TLocal is object and we have the concrete type, use it
            DebuggerSnapshotSerializer.Serialize(value, type ?? typeof(TLocal), name, _jsonWriter);
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
                    ExitAsyncMethodStart(ref info);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ExitMethodStart<TReturnOrException>(ref CaptureInfo<TReturnOrException> info)
        {
            CaptureStaticFields(ref info);

            if (info.MemberKind == ScopeMemberKind.Exception && info.Value != null)
            {
                CaptureException(info.Value as Exception);
            }
            else if (info.MemberKind == ScopeMemberKind.Return)
            {
                CaptureLocal(info.Value, "@return");
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
                CaptureArgument(argumentValue, argument.Name, argument.FieldType);
                hasArgument = true;
            }

            return hasArgument;
        }

        private void CaptureAsyncMethodLocals(AsyncHelper.FieldInfoNameSanitized[] asyncMethodHoistedLocals, object moveNextInvocationTarget)
        {
            // In the async scenario MethodMetadataInfo stores locals from MoveNext's localVarSig,
            // which isn't enough because we need to extract more locals that may be hoisted in the builder object
            // and we need to remove some locals that exist in the localVarSig but are just part of the async machinery and do not represent actual variables in the user's code.
            for (var index = 0; index < asyncMethodHoistedLocals.Length; index++)
            {
                ref var local = ref asyncMethodHoistedLocals[index];
                if (local == default)
                {
                    continue;
                }

                var localValue = local.Field.GetValue(moveNextInvocationTarget);
                CaptureLocal(localValue, local.SanitizedName, local.Field.FieldType);
            }
        }

        private void ExitAsyncMethodStart<T>(ref CaptureInfo<T> info)
        {
            ExitMethodStart(ref info);
            ExitAsyncMethodLogLocals(info.AsyncCaptureInfo.HoistedLocals, info.AsyncCaptureInfo.MoveNextInvocationTarget);
        }

        private void ExitAsyncMethodLogLocals(AsyncHelper.FieldInfoNameSanitized[] hoistedLocals, object moveNextInvocationTarget)
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
                CaptureLocal(localValue, local.SanitizedName, local.Field.FieldType);
            }
        }

        internal void CaptureBeginLine<T>(ref CaptureInfo<T> info)
        {
            StartLines(info.LineCaptureInfo.LineNumber);
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
            CaptureStaticFields(ref info);
            EndReturn(info.HasLocalOrArgument.Value);
        }

        internal void ProcessQueue<TCapture>(ref CaptureInfo<TCapture> captureInfo)
        {
            if (CaptureBehaviour == CaptureBehaviour.Delayed)
            {
                switch (captureInfo.MethodState)
                {
                    case MethodState.EntryEnd:
                        CaptureEntryMethodStartMarker(ref captureInfo);
                        break;
                    case MethodState.ExitEnd:
                        CaptureExitMethodStartMarker(ref captureInfo);
                        break;
                    case MethodState.EndLine:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(captureInfo.MethodState), captureInfo.MethodState, null);
                }

                CaptureScopeMembers(MethodScopeMembers.Members);
            }
        }

        internal void CaptureScopeMembers(ScopeMember[] members)
        {
            foreach (var member in members)
            {
                if (member.Type == null)
                {
                    // ArrayPool can allocate more items than we need, if "Type == null", this mean we can exit the loop
                    break;
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
        internal string FinalizeLineSnapshot<T>(string probeId, string message, ref CaptureInfo<T> info, List<EvaluationError> evaluationErrors = null)
        {
            using (this)
            {
                var methodName = info.MethodState == MethodState.EndLineAsync
                                     ? info.AsyncCaptureInfo.KickoffMethod?.Name
                                     : info.Method?.Name;

                var typeFullName = info.MethodState == MethodState.EndLineAsync
                                       ? info.AsyncCaptureInfo.KickoffInvocationTargetType?.FullName
                                       : info.InvocationTargetType?.FullName;

                AddEvaluationErrors(evaluationErrors).
                AddProbeInfo(
                        probeId,
                        info.LineCaptureInfo.LineNumber,
                        info.LineCaptureInfo.ProbeFilePath)
                   .FinalizeSnapshot(
                        methodName,
                        typeFullName,
                        _startTime,
                        info.LineCaptureInfo.ProbeFilePath,
                        message);

                var snapshot = GetSnapshotJson();
                return snapshot;
            }
        }

        internal string FinalizeMethodSnapshot<T>(string probeId, string message, ref CaptureInfo<T> info, List<EvaluationError> evaluationErrors = null)
        {
            using (this)
            {
                var methodName = info.MethodState == MethodState.ExitEndAsync
                                     ? info.AsyncCaptureInfo.KickoffMethod?.Name
                                     : info.Method?.Name;

                var typeFullName = info.MethodState == MethodState.ExitEndAsync
                                       ? info.AsyncCaptureInfo.KickoffInvocationTargetType?.FullName
                                       : info.InvocationTargetType?.FullName;
                AddEvaluationErrors(evaluationErrors).
                AddProbeInfo(
                        probeId,
                        methodName,
                        typeFullName)
                   .FinalizeSnapshot(
                        methodName,
                        typeFullName,
                        _startTime,
                        null,
                        message);

                var snapshot = GetSnapshotJson();
                return snapshot;
            }
        }

        internal void FinalizeSnapshot(string methodName, string typeFullName, DateTimeOffset? startTime, string probeFilePath, string message)
        {
            AddStackInfo()
            .EndSnapshot(startTime)
            .EndDebugger()
            .AddLoggerInfo(methodName, typeFullName, probeFilePath)
            .AddGeneralInfo(LiveDebugger.Instance?.ServiceName, null, null) // internal ticket ID 929
            .AddMessage(message)
            .Complete();
        }

        internal DebuggerSnapshotCreator AddEvaluationErrors(List<EvaluationError> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return this;
            }

            _jsonWriter.WritePropertyName("errors");
            _jsonWriter.WriteStartArray();
            foreach (var error in errors)
            {
                _jsonWriter.WriteStartObject();
                _jsonWriter.WritePropertyName("expression");
                _jsonWriter.WriteValue(error.Expression);
                _jsonWriter.WritePropertyName("message");
                _jsonWriter.WriteValue(error.Message);
                _jsonWriter.WriteEndObject();
            }

            _jsonWriter.WriteEndArray();
            return this;
        }

        internal DebuggerSnapshotCreator AddProbeInfo<T>(string probeId, T methodNameOrLineNumber, string typeFullNameOrFilePath)
        {
            _jsonWriter.WritePropertyName("probe");
            _jsonWriter.WriteStartObject();

            _jsonWriter.WritePropertyName("id");
            _jsonWriter.WriteValue(probeId);

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
                _jsonWriter.WriteValue(methodNameOrLineNumber);
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

            // todo
            _jsonWriter.WritePropertyName("ddtags");
            _jsonWriter.WriteValue(UnknownValue);

            _jsonWriter.WritePropertyName("dd.trace_id");
            _jsonWriter.WriteValue(traceId);

            _jsonWriter.WritePropertyName("dd.span_id");
            _jsonWriter.WriteValue(spanId);

            return this;
        }

        public DebuggerSnapshotCreator AddMessage(string message)
        {
            message ??= GenerateDefaultMessage();
            _jsonWriter.WritePropertyName("message");
            _jsonWriter.WriteValue(message);
            return this;
        }

        public DebuggerSnapshotCreator Complete()
        {
            _jsonWriter.WriteEndObject();
            return this;
        }

        private string GenerateDefaultMessage()
        {
            _jsonUnderlyingString.Append("}");
            var snapshotObject = JsonConvert.DeserializeObject<Snapshot>(_jsonUnderlyingString.ToString());
            _jsonUnderlyingString.Remove(_jsonUnderlyingString.Length - 1, 1);
            var message = SnapshotSummary.FormatMessage(snapshotObject);
            return message;
        }

        internal string GetSnapshotJson()
        {
            return StringBuilderCache.GetStringAndRelease(_jsonUnderlyingString);
        }

        public void Dispose()
        {
            try
            {
                MethodScopeMembers.Reset();
                _methodScopeMembers = null;
                _jsonWriter?.Close();
            }
            catch
            {
                // ignored
            }
        }
    }
}
