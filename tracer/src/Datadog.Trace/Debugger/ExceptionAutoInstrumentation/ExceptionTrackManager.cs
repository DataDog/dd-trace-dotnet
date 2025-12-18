// <copyright file="ExceptionTrackManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal sealed class ExceptionTrackManager : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ExceptionTrackManager>();
        private readonly ConcurrentDictionary<ExceptionIdentifier, TrackedExceptionCase> _trackedExceptionCases;
        private readonly ConcurrentQueue<Exception> _exceptionProcessQueue;
        private readonly SemaphoreSlim _workAvailable;
        private readonly TaskCompletionSource<bool> _processExit;
        private readonly ExceptionCaseScheduler _exceptionsScheduler;
        private readonly int _maxFramesToCapture;
        private readonly TimeSpan _rateLimit;
        private readonly BasicCircuitBreaker _reportingCircuitBreaker;
        private readonly CachedItems _evaluateWithRootSpanCases;
        private readonly CachedItems _cachedInvalidatedCases;
        private readonly Task? _exceptionProcessorTask;
        private readonly bool _isInitialized;

        private ExceptionTrackManager(ExceptionReplaySettings settings)
        {
            _trackedExceptionCases = new();
            _exceptionProcessQueue = new();
            _workAvailable = new(0, int.MaxValue);
            _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _exceptionsScheduler = new();
            _maxFramesToCapture = settings.MaximumFramesToCapture;
            _rateLimit = settings.RateLimit;
            _reportingCircuitBreaker = new(settings.MaxExceptionAnalysisLimit, TimeSpan.FromSeconds(1));
            _evaluateWithRootSpanCases = new();
            _cachedInvalidatedCases = new();

            _exceptionProcessorTask = Task.Run(StartExceptionProcessingAsync);
            _ = _exceptionProcessorTask.ContinueWith(
                t => Log.Error(t?.Exception, "Exception processor crashed"),
                TaskContinuationOptions.OnlyOnFaulted);

            IsEditAndContinueFeatureEnabled = IsEnCFeatureEnabled();
            _isInitialized = true;
        }

        internal static event Action<ExceptionIdentifier>? ExceptionCaseInstrumented;

        internal static bool IsEditAndContinueFeatureEnabled { get; private set; }

        internal static ExceptionTrackManager Create(ExceptionReplaySettings settings)
        {
            return new ExceptionTrackManager(settings);
        }

        private async Task StartExceptionProcessingAsync()
        {
            var exitTask = _processExit.Task;

            while (!exitTask.IsCompleted)
            {
                var completed = await Task.WhenAny(_workAvailable.WaitAsync(), exitTask).ConfigureAwait(false);
                if (completed == exitTask)
                {
                    break;
                }

                while (_exceptionProcessQueue.TryDequeue(out var exception))
                {
                    if (exitTask.IsCompleted)
                    {
                        return;
                    }

                    try
                    {
                        ProcessException(exception, 0, ErrorOriginKind.HttpRequestFailure, rootSpan: null);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "An exception was thrown while processing an exception for tracking from background thread. Exception = {Exception}", exception.ToString());
                    }
                }
            }
        }

        public void Report(Span span, Exception? exception)
        {
            if (!_isInitialized)
            {
                Log.Information(exception, "Exception Track Manager is not initialized yet. Skipping the processing of an exception. Exception = {Exception}, Span = {Span}", exception?.ToString(), span);
                SetDiagnosticTag(span, ExceptionReplayDiagnosticTagNames.ExceptionTrackManagerNotInitialized, 0);
                return;
            }

            if (exception == null || !IsSupportedExceptionType(exception))
            {
                Log.Information(exception, "Skipping the processing of the exception. Exception = {Exception}, Span = {Span}", exception?.ToString(), span.ToString());

                var failureReason = exception == null ? ExceptionReplayDiagnosticTagNames.ExceptionObjectIsNull : ExceptionReplayDiagnosticTagNames.NonSupportedExceptionType;
                SetDiagnosticTag(span, failureReason, 0);
                return;
            }

            try
            {
                ReportInternal(exception, ErrorOriginKind.HttpRequestFailure, span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An exception was thrown while processing an exception for tracking. Exception = {Exception}, Span = {Span}", exception.ToString(), span.ToString());
            }
        }

        private void ReportInternal(Exception exception, ErrorOriginKind errorOrigin, Span rootSpan)
        {
            var exToString = exception.ToString();
            var normalizedExHash = ExceptionNormalizer.Instance.NormalizeAndHashException(exToString, exception.GetType().Name, exception.InnerException?.GetType().Name);

            if (CachedDoneExceptions.Contains(normalizedExHash))
            {
                // Quick exit.
                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.CachedDoneExceptionCase, normalizedExHash);
                return;
            }

            if (_cachedInvalidatedCases.Contains(normalizedExHash))
            {
                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.CachedInvalidatedExceptionCase, normalizedExHash);

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Encountered an exception that resides in invalidated cache, exception: {Exception}", exToString);
                }

                return;
            }

            if (_evaluateWithRootSpanCases.Contains(normalizedExHash))
            {
                Log.Information("Encountered an exception that should be handled with root span, exception: {Exception}", exToString);
                ProcessException(exception, normalizedExHash, errorOrigin, rootSpan);
                return;
            }

            if (_reportingCircuitBreaker.Trip() == CircuitBreakerState.Opened)
            {
                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.CircuitBreakerIsOpen, normalizedExHash);

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(exception, "The circuit breaker is opened, skipping the processing of an exception.");
                }

                return;
            }

            var nonEmptyShadowStack = ShadowStackHolder.IsShadowStackTrackingEnabled && ShadowStackHolder.ShadowStack!.ContainsReport(exception);
            if (nonEmptyShadowStack)
            {
                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.Eligible, normalizedExHash);
                ProcessException(exception, normalizedExHash, errorOrigin, rootSpan);
            }
            else
            {
                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NotEligible, normalizedExHash);
                _exceptionProcessQueue.Enqueue(exception);
                _workAvailable.Release();
            }
        }

        private void ProcessException(Exception exception, int normalizedExHash, ErrorOriginKind errorOrigin, Span? rootSpan)
        {
            var allParticipatingFrames = GetAllExceptionRelatedStackFrames(exception);
            var allParticipatingFramesFlattened = allParticipatingFrames.GetAllFlattenedFrames().Reverse().ToArray();

            normalizedExHash = normalizedExHash != 0 ? normalizedExHash : ExceptionNormalizer.Instance.NormalizeAndHashException(exception.ToString(), exception.GetType().Name, exception.InnerException?.GetType().Name);

            if (allParticipatingFramesFlattened.Length == 0)
            {
                if (rootSpan != null)
                {
                    SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NoFramesToInstrument, normalizedExHash);
                    _cachedInvalidatedCases.Add(normalizedExHash);
                    _evaluateWithRootSpanCases.Remove(normalizedExHash);
                }
                else
                {
                    _evaluateWithRootSpanCases.Add(normalizedExHash);
                }

                return;
            }

            if (!ShouldReportException(exception, allParticipatingFramesFlattened))
            {
                if (rootSpan != null)
                {
                    SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NoCustomerFrames, normalizedExHash);
                    _cachedInvalidatedCases.Add(normalizedExHash);
                    _evaluateWithRootSpanCases.Remove(normalizedExHash);
                }
                else
                {
                    _evaluateWithRootSpanCases.Add(normalizedExHash);
                }

                Log.Information(exception, "Skipping the processing of an exception by Exception Debugging. All frames are 3rd party.");
                return;
            }

            var exceptionTypes = new HashSet<Type>();
            var currentFrame = allParticipatingFrames;
            var iterationLimit = 10;

            while (iterationLimit-- >= 0 && currentFrame != null)
            {
                exceptionTypes.Add(currentFrame.Exception.GetType());
                currentFrame = currentFrame.InnerFrame;
            }

            var exceptionId = new ExceptionIdentifier(exceptionTypes, allParticipatingFramesFlattened, errorOrigin);

            var trackedExceptionCase = _trackedExceptionCases.GetOrAdd(exceptionId, _ => new TrackedExceptionCase(exceptionId, exception.ToString(), _maxFramesToCapture));

            if (trackedExceptionCase.IsDone)
            {
                if (rootSpan != null)
                {
                    SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NonCachedDoneExceptionCase, normalizedExHash);
                    _evaluateWithRootSpanCases.Remove(normalizedExHash);
                }
            }
            else if (trackedExceptionCase.IsInvalidated)
            {
                if (rootSpan == null)
                {
                    _evaluateWithRootSpanCases.Add(normalizedExHash);

                    Log.Error("rootSpan is null while processing invalidated case. Should not happen. exception: {Exception}", exception.ToString());
                    return;
                }

                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.InvalidatedExceptionCase, normalizedExHash);

                var allFrames = StackTraceProcessor.ParseFrames(exception.ToString());
                var allProbes = trackedExceptionCase.ExceptionCase.Probes;
                var frameIndex = allFrames.Count - 1;
                var debugErrorPrefix = "_dd.debug.error";
                var assignIndex = 0;

                // Attach tags to the root span
                rootSpan.Tags.SetTag("error.debug_info_captured", "true");
                rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_hash", trackedExceptionCase.ErrorHash);
                rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_id", Guid.NewGuid().ToString());

                while (frameIndex >= 0)
                {
                    var participatingFrame = allFrames[frameIndex--];
                    var noCaptureReason = GetNoCaptureReason(participatingFrame, allProbes.FirstOrDefault(p => MethodMatcher.IsMethodMatch(participatingFrame, p.Method.Method)));

                    if (noCaptureReason != string.Empty)
                    {
                        TagMissingFrame(rootSpan, $"{debugErrorPrefix}.{assignIndex}.", participatingFrame, noCaptureReason);
                    }

                    assignIndex += 1;
                }

                _ = trackedExceptionCase.Revert(normalizedExHash, _rateLimit);
                _cachedInvalidatedCases.Add(normalizedExHash);
                _evaluateWithRootSpanCases.Remove(normalizedExHash);
            }
            else if (trackedExceptionCase.IsCollecting)
            {
                Log.Information("Exception case re-occurred, data can be collected. Exception : {FullName} {Message}.", exception.GetType().FullName, exception.Message);

                var shouldCheckWhyThereAreNoFrames = false;

                if (rootSpan == null)
                {
                    Log.Error("The RootSpan is null. Exception: {Exception}", exception.ToString());
                    shouldCheckWhyThereAreNoFrames = true;
                }

                if (!ShadowStackHolder.IsShadowStackTrackingEnabled)
                {
                    Log.Error("The shadow stack is not enabled, while processing IsCollecting state of an exception. Exception: {Exception}.", exception.ToString());
                    shouldCheckWhyThereAreNoFrames = true;
                }

                var resultCallStackTree = shouldCheckWhyThereAreNoFrames ? null : ShadowStackHolder.ShadowStack!.CreateResultReport(exceptionPath: exception);
                if (resultCallStackTree == null || !resultCallStackTree.Frames.Any())
                {
                    Log.Warning("ExceptionTrackManager: Checking why there are no frames captured for exception: {Exception}.", exception.ToString());

                    // Check if we failed to instrument all the probes.

                    if (trackedExceptionCase.ExceptionCase.Probes.Any(p => p.ProbeStatus == Status.RECEIVED || p.ProbeStatus == Status.INSTALLED))
                    {
                        // Determine if there are any errored probe statuses by P/Invoking the native for RECEIVED/INSTALLED probes.

                        var receivedOrRequestedRejitStatusProbeIds = trackedExceptionCase.ExceptionCase.Probes.Where(p => p.IsInstrumented && (p.ProbeStatus == Status.RECEIVED || p.ProbeStatus == Status.INSTALLED)).ToList();

                        if (receivedOrRequestedRejitStatusProbeIds.Any())
                        {
                            var statuses = DebuggerNativeMethods.GetProbesStatuses(receivedOrRequestedRejitStatusProbeIds.Select(p => p.ProbeId).ToArray());

                            // Update the status only if we're dealing with probes marked as ERRORs.
                            foreach (var status in statuses)
                            {
                                var probe = receivedOrRequestedRejitStatusProbeIds.FirstOrDefault(p => p.ProbeId == status.ProbeId);
                                if (probe != null)
                                {
                                    probe.ProbeStatus = status.Status;
                                    probe.ErrorMessage = status.Status is Status.ERROR ? status.ErrorMessage : null;
                                }
                            }
                        }
                    }

                    if (trackedExceptionCase.ExceptionCase.Probes.All(p => p.IsInstrumented && (p.ProbeStatus == Status.ERROR || p.ProbeStatus == Status.BLOCKED || p.ProbeStatus == Status.RECEIVED || p.MayBeOmittedFromCallStack)))
                    {
                        Log.Information("Invalidating the exception case of the empty stack tree since none of the methods were instrumented, for exception: {Name}, Message: {Message}, StackTrace: {StackTrace}", exception.GetType().Name, exception.Message, exception.StackTrace);
                        trackedExceptionCase.InvalidateCase();

                        _evaluateWithRootSpanCases.Add(normalizedExHash);

                        if (rootSpan != null)
                        {
                            SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.InvalidatedCase, normalizedExHash);
                        }
                    }
                    else
                    {
                        _evaluateWithRootSpanCases.Add(normalizedExHash);

                        if (rootSpan != null)
                        {
                            SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.EmptyCallStackTreeWhileCollecting, normalizedExHash);
                        }
                    }

                    return;
                }
                else
                {
                    if (rootSpan == null)
                    {
                        Log.Error("The RootSpan is null in the branch of extracting snapshots. Should not happen. Exception: {Exception}", exception.ToString());
                        return;
                    }

                    var exceptionCaptureId = Guid.NewGuid().ToString();

                    // Attach tags to the root span
                    var debugErrorPrefix = "_dd.debug.error";
                    rootSpan.Tags.SetTag("error.debug_info_captured", "true");
                    rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_hash", trackedExceptionCase.ErrorHash);
                    rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_id", exceptionCaptureId);

                    var @case = trackedExceptionCase.ExceptionCase;
                    var capturedFrames = resultCallStackTree.Frames;
                    var allFrames = StackTraceProcessor.ParseFrames(exception.ToString());
                    var frameIndex = allFrames.Count - 1;
                    var uploadedHeadFrame = false;

                    // Upload head frame
                    if (capturedFrames[0].MethodInfo.Method.Equals(@case.Probes[0].Method.Method))
                    {
                        while (frameIndex >= 0 && !MethodMatcher.IsMethodMatch(allFrames[frameIndex], capturedFrames[0].MethodInfo.Method))
                        {
                            // Just processing the frames until a match is found
                            frameIndex -= 1;
                        }

                        TagAndUpload(rootSpan, $"{debugErrorPrefix}.{frameIndex}.", capturedFrames[0], exceptionId: exceptionCaptureId, exceptionHash: trackedExceptionCase.ErrorHash, frameIndex: frameIndex);
                        uploadedHeadFrame = true;
                    }
                    else
                    {
                        // Missing head
                        var probe = @case.Probes[0];
                        var noCaptureReason = GetNoCaptureReason(probe.Method.Method.Name, probe);

                        if (noCaptureReason != string.Empty)
                        {
                            while (frameIndex >= 0 && !MethodMatcher.IsMethodMatch(allFrames[frameIndex], @case.Probes[0].Method.Method))
                            {
                                // Just processing the frames until a match is found
                                frameIndex -= 1;
                            }

                            TagMissingFrame(rootSpan, $"{debugErrorPrefix}.{frameIndex}.", probe.Method.Method.Name, noCaptureReason);
                        }
                    }

                    frameIndex = 0;
                    var capturedFrameIndex = capturedFrames.Count - 1;
                    var probeIndex = @case.Probes.Length - 1;
                    var capturedFrameIndexBound = uploadedHeadFrame ? 0 : -1;
                    var uploadFramesBound = _maxFramesToCapture;
                    var uploadedFrames = 0;
                    while (frameIndex < allFrames.Count && uploadedFrames < uploadFramesBound && probeIndex >= 0)
                    {
                        if (capturedFrameIndex <= capturedFrameIndexBound)
                        {
                            // No 'captured frames' left for matching
                            while (probeIndex >= 0)
                            {
                                var noCaptureReason = GetNoCaptureReason(@case.Probes[probeIndex].Method.Method.Name, @case.Probes[probeIndex]);

                                if (noCaptureReason != string.Empty)
                                {
                                    while (frameIndex < allFrames.Count && !MethodMatcher.IsMethodMatch(allFrames[frameIndex], @case.Probes[probeIndex].Method.Method))
                                    {
                                        // Just processing the frames until a match is found
                                        frameIndex += 1;
                                    }

                                    if (frameIndex >= allFrames.Count)
                                    {
                                        // Nothing left to match
                                        break;
                                    }

                                    TagMissingFrame(rootSpan, $"{debugErrorPrefix}.{frameIndex}.", @case.Probes[probeIndex].Method.Method.Name, noCaptureReason);
                                }

                                probeIndex -= 1;
                            }

                            break;
                        }

                        while (probeIndex >= 0 && !capturedFrames[capturedFrameIndex].MethodInfo.Method.Equals(@case.Probes[probeIndex].Method.Method))
                        {
                            // Determine if the frame is misleading (e.g duplicated on the stack thanks for ExceptionCaptureInfo.Throw)
                            var prevIndex = probeIndex + 1;
                            if (prevIndex < @case.Probes.Length && capturedFrames[capturedFrameIndex].MethodInfo.Method.Equals(@case.Probes[prevIndex].Method.Method) && @case.Probes[prevIndex].Method.IsMisleadMethod())
                            {
                                // The current captured frame is marked as misleading. Consider the previous probe as the 'current' probe for a proper matching in this extremely rare case
                                Log.Warning("Encountered misleading frame that is also recursive with exception: {Exception}, Method: {MethodName}", exception.ToString(), capturedFrames[capturedFrameIndex].MethodInfo.Method.GetFullName());
                                probeIndex += 1;
                                break;
                            }

                            var noCaptureReason = GetNoCaptureReason(@case.Probes[probeIndex].Method.Method.Name, @case.Probes[probeIndex]);

                            if (noCaptureReason != string.Empty)
                            {
                                while (frameIndex < allFrames.Count && !MethodMatcher.IsMethodMatch(allFrames[frameIndex], @case.Probes[probeIndex].Method.Method))
                                {
                                    // Just processing the frames until a match is found
                                    frameIndex += 1;
                                }

                                TagMissingFrame(rootSpan, $"{debugErrorPrefix}.{frameIndex}.", @case.Probes[probeIndex].Method.Method.Name, noCaptureReason);
                            }

                            probeIndex -= 1;
                        }

                        if (probeIndex < 0)
                        {
                            // We exhausted the probes array, nothing left to match
                            break;
                        }

                        while (frameIndex < allFrames.Count && !MethodMatcher.IsMethodMatch(allFrames[frameIndex], capturedFrames[capturedFrameIndex].MethodInfo.Method))
                        {
                            frameIndex += 1;
                        }

                        if (frameIndex >= allFrames.Count)
                        {
                            // We exhausted the whole frames array, nothing left to match
                            break;
                        }

                        TagAndUpload(rootSpan, $"{debugErrorPrefix}.{frameIndex}.", capturedFrames[capturedFrameIndex], exceptionId: exceptionCaptureId, exceptionHash: trackedExceptionCase.ErrorHash, frameIndex: frameIndex);
                        capturedFrameIndex -= 1;
                        probeIndex -= 1;
                        frameIndex += 1;
                        uploadedFrames += 1;
                    }

                    Log.Information("Reverting an exception case for exception: {Name}, Message: {Message}, StackTrace: {StackTrace}", exception.GetType().Name, exception.Message, exception.StackTrace);

                    if (trackedExceptionCase.Revert(normalizedExHash, _rateLimit))
                    {
                        CachedDoneExceptions.Add(normalizedExHash);
                        _exceptionsScheduler.Schedule(trackedExceptionCase, _rateLimit);
                    }
                }
            }
            else
            {
                Log.Information("New exception case occurred, initiating data collection for exception: {Name}, Message: {Message}, StackTrace: {StackTrace}", exception.GetType().Name, exception.Message, exception.StackTrace);
                trackedExceptionCase.Instrument();
                ExceptionCaseInstrumented?.Invoke(trackedExceptionCase.ExceptionIdentifier);

                if (rootSpan != null)
                {
                    SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NewCase, normalizedExHash);
                }
            }
        }

        private string GetNoCaptureReason(string methodName, ExceptionReplayProbe? probe)
        {
            var noCaptureReason = string.Empty;

            if (probe != null)
            {
                if (probe.MayBeOmittedFromCallStack)
                {
                    // The process is spawned with `COMPLUS_ForceEnc` & the module of the method is non-optimized.
                    noCaptureReason = $"The method {methodName} could not be captured because the process is spawned with Edit and Continue feature turned on and the module is compiled as Debug. Set the environment variable `COMPLUS_ForceEnc` to `0`. For further info, visit: https://github.com/dotnet/runtime/issues/91963.";
                }
                else if (probe.ProbeStatus == Status.ERROR)
                {
                    // Frame is failed to instrument.
                    noCaptureReason = $"The method {methodName} has failed in instrumentation. Failure reason: {probe.ErrorMessage}";
                }
                else if (probe.ProbeStatus == Status.RECEIVED)
                {
                    // Frame is failed to instrument.
                    noCaptureReason = $"The method {methodName} could not be found.";
                }
                else if (probe.Method.IsMisleadMethod())
                {
                    noCaptureReason = $"This frame of {methodName} is a duplication, due to `ExceptionDispatchInfo.Throw()`.";
                }
            }

            return noCaptureReason;
        }

        private string GetNoCaptureReasonForFrame(ParticipatingFrame frame)
        {
            var noCaptureReason = string.Empty;

            if (frame.State == ParticipatingFrameState.Blacklist)
            {
                noCaptureReason = $"The method {frame.Method.GetFullyQualifiedName()} is blacklisted.";
            }

            if (frame.Method.IsAbstract)
            {
                noCaptureReason = $"The method {frame.Method.GetFullyQualifiedName()} is abstract.";
            }

            return noCaptureReason;
        }

        private void TagAndUpload(Span span, string tagPrefix, ExceptionStackNodeRecord record, string exceptionId, string exceptionHash, int frameIndex)
        {
            var method = record.MethodInfo.Method;
            var snapshotId = record.SnapshotId;
            var probeId = record.ProbeId;
            var snapshot = record.Snapshot;

            span.Tags.SetTag(tagPrefix + "frame_data.function", method.Name);
            span.Tags.SetTag(tagPrefix + "frame_data.class_name", method.DeclaringType?.Name);
            span.Tags.SetTag(tagPrefix + "snapshot_id", snapshotId);

            snapshot = snapshot
                      .Replace(ExceptionReplaySnapshotCreator.ExceptionCaptureId, exceptionId)
                      .Replace(ExceptionReplaySnapshotCreator.ExceptionHash, exceptionHash)
                      .Replace(ExceptionReplaySnapshotCreator.FrameIndex, frameIndex.ToString());
            DebuggerManager.Instance.ExceptionReplay?.AddSnapshot(probeId, snapshot);
        }

        private void TagMissingFrame(Span span, string tagPrefix, string method, string reason)
        {
            span.Tags.SetTag(tagPrefix + "frame_data.name", method);
            span.Tags.SetTag(tagPrefix + "no_capture_reason", reason);
        }

        private bool ShouldReportException(Exception ex, ParticipatingFrame[] framesToRejit)
        {
            try
            {
                return AtLeastOneFrameBelongToUserCode();
            }
            catch
            {
                // When in doubt, report it.
                return true;
            }

            bool AtLeastOneFrameBelongToUserCode() => framesToRejit.Any(f => FrameFilter.IsUserCode(f));
        }

        private bool IsSupportedExceptionType(Exception ex) =>
            IsSupportedExceptionType(ex.GetType());

        /// <summary>
        /// In .NET 6+ there's a bug that prevents Rejit-related APIs to work properly when Edit and Continue feature is turned on.
        /// See https://github.com/dotnet/runtime/issues/91963 for additional details.
        /// </summary>
        private bool IsEnCFeatureEnabled()
        {
            var encEnabled = EnvironmentHelpers.GetEnvironmentVariable("COMPLUS_ForceEnc");
            return !string.IsNullOrEmpty(encEnabled) && (encEnabled == "1" || encEnabled == "true");
        }

        public bool IsSupportedExceptionType(Type ex) =>
            ex != typeof(BadImageFormatException) &&
            ex != typeof(InvalidProgramException) &&
            ex != typeof(TypeInitializationException) &&
            ex != typeof(TypeLoadException) &&
            // Both OutOfMemoryException and ThreadAbortException may be thrown anywhere from non-deterministic reasons, which means it won't
            // necessarily re-occur with the same callstack. We should not attempt to rejit methods nor track these exceptions.
            // ThreadAbortException is particularly problematic because is it often thrown in legacy ASP.NET apps whenever
            // there is an HTTP Redirect. See also https://stackoverflow.com/questions/2777105/why-response-redirect-causes-system-threading-threadabortexception
            ex != typeof(OutOfMemoryException) &&
            ex != typeof(ThreadAbortException);

        public void Dispose()
        {
            _processExit.TrySetResult(true);
            _reportingCircuitBreaker.Dispose();

            foreach (var trackedExceptionCase in _trackedExceptionCases.Values)
            {
                try
                {
                    if (!trackedExceptionCase.Revert(0, _rateLimit))
                    {
                        ExceptionCaseInstrumentationManager.Revert(trackedExceptionCase.ExceptionCase); // Force revert
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "ExceptionTrackManager: An exception was thrown while calling Revert on given exception case {ExceptionIdentifier}.", trackedExceptionCase.ExceptionIdentifier);
                }
            }

            try
            {
                _exceptionProcessorTask?.Wait();
            }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is TaskCanceledException))
            {
                Log.Information("ExceptionTrackManager: Background task was canceled as part of the stop process.");
            }
            catch (Exception e)
            {
                Log.Error(e, "ExceptionTrackManager: An exception was thrown while waiting for the background task to stop processing.");
            }
            finally
            {
                _workAvailable.Dispose();
                _trackedExceptionCases.Clear();
            }
        }

        public ExceptionRelatedFrames GetAllExceptionRelatedStackFrames(Exception exception)
        {
            return CreateExceptionPath(exception, true);

            ExceptionRelatedFrames CreateExceptionPath(Exception e, bool isTopFrame)
            {
                var frames = GetParticipatingFrames(new StackTrace(e, false), isTopFrame, ParticipatingFrameState.Default);

                ExceptionRelatedFrames? innerFrame = null;

                // the first inner exception in the inner exceptions list of the aggregate exception is similar to the inner exception`
                innerFrame = e.InnerException != null ? CreateExceptionPath(e.InnerException, false) : null;

                return new ExceptionRelatedFrames(e, frames.ToArray(), innerFrame);
            }
        }

        /// <summary>
        /// Getting a stack trace and creating list of ParticipatingFrame according to requested parameters and filters
        /// </summary>
        /// <param name="stackTrace">Stack trace to get frames from</param>
        /// <param name="isTopFrame">If it's a top frame, we should skip on the above method (e.g. ASP Net methods)</param>
        /// <param name="defaultState">Default state of all method that are not <see cref="ParticipatingFrameState.Blacklist"/> </param>
        /// <returns>All the frames of the exception.</returns>
        public IEnumerable<ParticipatingFrame> GetParticipatingFrames(StackTrace stackTrace, bool isTopFrame, ParticipatingFrameState defaultState)
        {
            // We hit the `public static void Reverse<T>(this Span<T> span)` function here on .NET 3.1+
            // This causes this to fail to compile.
            // Just specifying .AsEnumerable for now to minimize changes
            var frames = isTopFrame
                             ? stackTrace.GetFrames()?.
                                          AsEnumerable().
                                          Reverse().
                                          SkipWhile(frame =>
                                          {
                                              MethodBase? method;

                                              try
                                              {
                                                  method = frame?.GetMethod();
                                                  var token = method?.MetadataToken;
                                              }
                                              catch
                                              {
                                                  return true;
                                              }

                                              if (method == null)
                                              {
                                                  return true;
                                              }

                                              return FrameFilter.ShouldSkipNamespaceIfOnTopOfStack(method);
                                          }).
                                          Reverse().
                                          GetAsyncFriendlyFrameMethods()
                             : stackTrace.GetFrames()?.GetAsyncFriendlyFrameMethods();

            if (frames == null)
            {
                yield break;
            }

            foreach (var frame in frames)
            {
                if (frame == null)
                {
                    continue;
                }

                MethodBase? method;
                try
                {
                    method = frame.GetMethod();
                    var token = method?.MetadataToken;
                }
                catch
                {
                    continue;
                }

                if (method == null)
                {
                    continue;
                }

                var assembly = method.Module.Assembly;
                var assemblyName = assembly.GetName().Name;

                if (assemblyName != null && AssemblyFilter.IsDatadogAssembly(assemblyName))
                {
                    continue;
                }

                if (ShouldSkip(method))
                {
                    continue;
                }

                if (FrameFilter.IsBlockList(method))
                {
                    yield return new ParticipatingFrame(frame, ParticipatingFrameState.Blacklist);
                }
                else
                {
                    yield return new ParticipatingFrame(frame, defaultState);
                }
            }
        }

        private bool ShouldSkip(MethodBase method)
        {
            try
            {
                // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                return (method.MethodImplementationFlags & MethodImplAttributes.AggressiveInlining) == MethodImplAttributes.AggressiveInlining;
            }
            catch
            {
                return true;
            }
        }

        private void SetDiagnosticTag(Span span, string exceptionPhase, int exceptionHash)
        {
            if (exceptionPhase is ExceptionReplayDiagnosticTagNames.CachedInvalidatedExceptionCase or ExceptionReplayDiagnosticTagNames.CachedDoneExceptionCase or ExceptionReplayDiagnosticTagNames.NonCachedDoneExceptionCase)
            {
                return;
            }

            var noCaptureReason = exceptionPhase switch
            {
                ExceptionReplayDiagnosticTagNames.Eligible => string.Empty,
                ExceptionReplayDiagnosticTagNames.NoCustomerFrames or ExceptionReplayDiagnosticTagNames.NoFramesToInstrument => NoCaptureReason.OnlyThirdPartyCode,
                ExceptionReplayDiagnosticTagNames.InvalidatedCase or ExceptionReplayDiagnosticTagNames.InvalidatedExceptionCase => NoCaptureReason.InstrumentationFailure,
                ExceptionReplayDiagnosticTagNames.NonSupportedExceptionType => NoCaptureReason.NonSupportedExceptionType,
                ExceptionReplayDiagnosticTagNames.NotEligible or ExceptionReplayDiagnosticTagNames.NewCase => NoCaptureReason.FirstOccurrence,
                _ => NoCaptureReason.GeneralError,
            };

            if (noCaptureReason != string.Empty)
            {
                span.Tags.SetTag("error.debug_info_captured", "true");
                span.Tags.SetTag("_dd.debug.error.no_capture_reason", noCaptureReason);
            }

            span.Tags.SetTag("_dd.di._er", exceptionPhase);
            span.Tags.SetTag("_dd.di._eh", exceptionHash.ToString());
        }
    }
}
