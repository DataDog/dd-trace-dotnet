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
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.Vendors.Serilog.Events;
using ProbeInfo = Datadog.Trace.Debugger.Expressions.ProbeInfo;
using ProbeLocation = Datadog.Trace.Debugger.Expressions.ProbeLocation;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionTrackManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ExceptionTrackManager>();
        private static readonly ConcurrentDictionary<ExceptionIdentifier, TrackedExceptionCase> TrackedExceptionCases = new();
        private static readonly ConcurrentQueue<Exception> ExceptionProcessQueue = new();
        private static readonly SemaphoreSlim WorkAvailable = new(0, int.MaxValue);
        private static readonly CancellationTokenSource Cts = new();
        private static readonly ExceptionCaseScheduler ExceptionsScheduler = new();
        private static readonly ExceptionNormalizer ExceptionNormalizer = new();
        private static readonly int MaxFramesToCapture = ExceptionDebugging.Settings.MaximumFramesToCapture;
        private static readonly TimeSpan RateLimit = ExceptionDebugging.Settings.RateLimit;
        private static readonly BasicCircuitBreaker ReportingCircuitBreaker = new(ExceptionDebugging.Settings.MaxExceptionAnalysisLimit, TimeSpan.FromSeconds(1));
        private static readonly CachedItems EvaluateWithRootSpanCases = new();
        private static readonly CachedItems CachedInvalidatedCases = new();
        private static Task? _exceptionProcessorTask;
        private static bool _isInitialized;

        internal static bool IsEditAndContinueFeatureEnabled { get; private set; }

        private static async Task StartExceptionProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await WorkAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);

                while (ExceptionProcessQueue.TryDequeue(out var exception))
                {
                    ProcessException(exception, 0, ErrorOriginKind.HttpRequestFailure, rootSpan: null);
                }
            }
        }

        public static void Report(Span span, Exception? exception)
        {
            if (!_isInitialized)
            {
                Log.Information(exception, "Exception Track Manager is not initialized yet. Skipping the processing of an exception. Exception = {Exception}, Span = {Span}", exception?.ToString(), span);
                SetDiagnosticTag(span, ExceptionReplayDiagnosticTagNames.ExceptionTrackManagerNotInitialized, 0);
                return;
            }

            // For V1 of Exception Debugging, we only care about exceptions propagating up the stack
            // and marked as error by the service entry/root span.
            if (span.IsRootSpan == false || exception == null || !IsSupportedExceptionType(exception))
            {
                Log.Information(exception, "Skipping the processing of the exception. Exception = {Exception}, Span = {Span}", exception?.ToString(), span.ToString());

                var failureReason =
                    span.IsRootSpan == false ? ExceptionReplayDiagnosticTagNames.NotRootSpan :
                                    exception == null ? ExceptionReplayDiagnosticTagNames.ExceptionObjectIsNull : ExceptionReplayDiagnosticTagNames.NonSupportedExceptionType;
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

        private static void ReportInternal(Exception exception, ErrorOriginKind errorOrigin, Span rootSpan)
        {
            var exToString = exception.ToString();
            var normalizedExHash = ExceptionNormalizer.NormalizeAndHashException(exToString, exception.GetType().Name, exception.InnerException?.GetType().Name);

            if (CachedDoneExceptions.Contains(normalizedExHash))
            {
                // Quick exit.
                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.CachedDoneExceptionCase, normalizedExHash);
                return;
            }

            if (CachedInvalidatedCases.Contains(normalizedExHash))
            {
                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.CachedInvalidatedExceptionCase, normalizedExHash);

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Encountered an exception that resides in invalidated cache, exception: {Exception}", exToString);
                }

                return;
            }

            if (EvaluateWithRootSpanCases.Contains(normalizedExHash))
            {
                Log.Information("Encountered an exception that should be handled with root span, exception: {Exception}", exToString);
                ProcessException(exception, normalizedExHash, errorOrigin, rootSpan);
                return;
            }

            if (ReportingCircuitBreaker.Trip() == CircuitBreakerState.Opened)
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
                ExceptionProcessQueue.Enqueue(exception);
                WorkAvailable.Release();
            }
        }

        private static void ProcessException(Exception exception, int normalizedExHash, ErrorOriginKind errorOrigin, Span? rootSpan)
        {
            var allParticipatingFrames = GetAllExceptionRelatedStackFrames(exception);
            var allParticipatingFramesFlattened = allParticipatingFrames.GetAllFlattenedFrames().Reverse().ToArray();

            normalizedExHash = normalizedExHash != 0 ? normalizedExHash : ExceptionNormalizer.NormalizeAndHashException(exception.ToString(), exception.GetType().Name, exception.InnerException?.GetType().Name);

            if (allParticipatingFramesFlattened.Length == 0)
            {
                if (rootSpan != null)
                {
                    SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NoFramesToInstrument, normalizedExHash);
                    CachedInvalidatedCases.Add(normalizedExHash);
                    EvaluateWithRootSpanCases.Remove(normalizedExHash);
                }
                else
                {
                    EvaluateWithRootSpanCases.Add(normalizedExHash);
                }

                return;
            }

            if (!ShouldReportException(exception, allParticipatingFramesFlattened))
            {
                if (rootSpan != null)
                {
                    SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NoCustomerFrames, normalizedExHash);
                    CachedInvalidatedCases.Add(normalizedExHash);
                    EvaluateWithRootSpanCases.Remove(normalizedExHash);
                }
                else
                {
                    EvaluateWithRootSpanCases.Add(normalizedExHash);
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

            var trackedExceptionCase = TrackedExceptionCases.GetOrAdd(exceptionId, _ => new TrackedExceptionCase(exceptionId, exception.ToString()));

            if (trackedExceptionCase.IsDone)
            {
                if (rootSpan != null)
                {
                    SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NonCachedDoneExceptionCase, normalizedExHash);
                    EvaluateWithRootSpanCases.Remove(normalizedExHash);
                }
            }
            else if (trackedExceptionCase.IsInvalidated)
            {
                if (rootSpan == null)
                {
                    EvaluateWithRootSpanCases.Add(normalizedExHash);

                    Log.Error("rootSpan is null while processing invalidated case. Should not happen. exception: {Exception}", exception.ToString());
                    return;
                }

                SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.InvalidatedExceptionCase, normalizedExHash);

                var allFrames = trackedExceptionCase.ExceptionCase.ExceptionId.StackTrace;
                var allProbes = trackedExceptionCase.ExceptionCase.Probes;
                var frameIndex = allFrames.Length - 1;
                var debugErrorPrefix = "_dd.debug.error";
                var assignIndex = 0;

                // Attach tags to the root span
                rootSpan.Tags.SetTag("error.debug_info_captured", "true");
                rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_hash", trackedExceptionCase.ErrorHash);
                rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_id", Guid.NewGuid().ToString());

                while (frameIndex >= 0)
                {
                    var participatingFrame = allFrames[frameIndex--];
                    var noCaptureReason = GetNoCaptureReason(participatingFrame, allProbes.FirstOrDefault(p => p.Method.Equals(participatingFrame.MethodIdentifier)));

                    if (noCaptureReason != string.Empty)
                    {
                        TagMissingFrame(rootSpan, $"{debugErrorPrefix}.{assignIndex}.", participatingFrame.Method, noCaptureReason);
                    }

                    assignIndex += 1;
                }

                _ = trackedExceptionCase.Revert(normalizedExHash);
                CachedInvalidatedCases.Add(normalizedExHash);
                EvaluateWithRootSpanCases.Remove(normalizedExHash);
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

                        EvaluateWithRootSpanCases.Add(normalizedExHash);

                        if (rootSpan != null)
                        {
                            SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.InvalidatedCase, normalizedExHash);
                        }
                    }
                    else
                    {
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
                        Log.Error("The RootSpan is null in the branch of extracing snapshots. Should not happen. Exception: {Exception}", exception.ToString());
                        return;
                    }

                    // Attach tags to the root span
                    var debugErrorPrefix = "_dd.debug.error";
                    rootSpan.Tags.SetTag("error.debug_info_captured", "true");
                    rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_hash", trackedExceptionCase.ErrorHash);
                    rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_id", Guid.NewGuid().ToString());

                    var @case = trackedExceptionCase.ExceptionCase;
                    var capturedFrames = resultCallStackTree.Frames;
                    var allFrames = @case.ExceptionId.StackTrace;

                    // Upload head frame
                    var frameIndex = 0;

                    while (frameIndex < allFrames.Length &&
                           !allFrames[frameIndex].Method.Equals(capturedFrames[0].MethodInfo.Method))
                    {
                        frameIndex += 1;
                    }

                    var frame = capturedFrames[0];
                    TagAndUpload(rootSpan, $"{debugErrorPrefix}.{allFrames.Length - frameIndex - 1}.", frame);

                    // Upload tail frames
                    frameIndex = allFrames.Length - 1;
                    var capturedFrameIndex = capturedFrames.Count - 1;
                    var assignIndex = 0;

                    while (capturedFrameIndex >= 1 && frameIndex >= 0)
                    {
                        frame = capturedFrames[capturedFrameIndex];

                        var participatingFrame = allFrames[frameIndex--];

                        if (!participatingFrame.Method.Equals(frame.MethodInfo.Method))
                        {
                            var noCaptureReason = GetNoCaptureReason(participatingFrame, @case.Probes.FirstOrDefault(p => p.Method.Equals(participatingFrame.MethodIdentifier)));

                            if (noCaptureReason != string.Empty)
                            {
                                TagMissingFrame(rootSpan, $"{debugErrorPrefix}.{assignIndex}.", participatingFrame.Method, noCaptureReason);
                            }

                            assignIndex += 1;
                            continue;
                        }

                        capturedFrameIndex -= 1;

                        var prefix = $"{debugErrorPrefix}.{assignIndex++}.";
                        TagAndUpload(rootSpan, prefix, frame);
                    }

                    // Upload missing frames
                    var maxFramesToCaptureIncludingHead = MaxFramesToCapture + 1;
                    if (capturedFrames.Count < maxFramesToCaptureIncludingHead &&
                        allFrames.Length > capturedFrames.Count)
                    {
                        frameIndex = allFrames.Length - 1;
                        var probesIndex = @case.Probes.Length - 1;
                        assignIndex = 0;

                        while (frameIndex >= 0 && maxFramesToCaptureIncludingHead > 0)
                        {
                            maxFramesToCaptureIncludingHead -= 1;
                            var participatingFrame = allFrames[frameIndex--];

                            var noCaptureReason = GetNoCaptureReasonForFrame(participatingFrame);

                            if (noCaptureReason == string.Empty && probesIndex >= 0)
                            {
                                noCaptureReason = GetNoCaptureReason(participatingFrame, @case.Probes[probesIndex--]);
                            }

                            if (noCaptureReason != string.Empty)
                            {
                                TagMissingFrame(rootSpan, $"{debugErrorPrefix}.{assignIndex}.", participatingFrame.Method, noCaptureReason);
                            }

                            assignIndex++;
                        }
                    }

                    Log.Information("Reverting an exception case for exception: {Name}, Message: {Message}, StackTrace: {StackTrace}", exception.GetType().Name, exception.Message, exception.StackTrace);

                    if (trackedExceptionCase.Revert(normalizedExHash))
                    {
                        CachedDoneExceptions.Add(normalizedExHash);
                        ExceptionsScheduler.Schedule(trackedExceptionCase, RateLimit);
                    }
                }
            }
            else
            {
                Log.Information("New exception case occurred, initiating data collection for exception: {Name}, Message: {Message}, StackTrace: {StackTrace}", exception.GetType().Name, exception.Message, exception.StackTrace);
                trackedExceptionCase.Instrument();

                if (rootSpan != null)
                {
                    SetDiagnosticTag(rootSpan, ExceptionReplayDiagnosticTagNames.NewCase, normalizedExHash);
                }
            }
        }

        private static string GetNoCaptureReason(ParticipatingFrame frame, ExceptionDebuggingProbe? probe)
        {
            var noCaptureReason = GetNoCaptureReasonForFrame(frame);

            if (noCaptureReason != string.Empty)
            {
                return noCaptureReason;
            }

            if (probe != null)
            {
                if (probe.MayBeOmittedFromCallStack)
                {
                    // The process is spawned with `COMPLUS_ForceEnc` & the module of the method is non-optimized.
                    noCaptureReason = $"The method {frame.Method.GetFullyQualifiedName()} could not be captured because the process is spawned with Edit and Continue feature turned on and the module is compiled as Debug. Set the environment variable `COMPLUS_ForceEnc` to `0`. For further info, visit: https://github.com/dotnet/runtime/issues/91963.";
                }
                else if (probe.ProbeStatus == Status.ERROR)
                {
                    // Frame is failed to instrument.
                    noCaptureReason = $"The method {frame.Method.GetFullyQualifiedName()} has failed in instrumentation. Failure reason: {probe.ErrorMessage}";
                }
                else if (probe.ProbeStatus == Status.RECEIVED)
                {
                    // Frame is failed to instrument.
                    noCaptureReason = $"The method {frame.Method.GetFullyQualifiedName()} could not be found.";
                }
            }

            return noCaptureReason;
        }

        private static string GetNoCaptureReasonForFrame(ParticipatingFrame frame)
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

        private static void TagAndUpload(Span span, string tagPrefix, ExceptionStackNodeRecord record)
        {
            var method = record.MethodInfo.Method;
            var snapshotId = record.SnapshotId;
            var probeId = record.ProbeId;
            var snapshot = record.Snapshot;

            span.Tags.SetTag(tagPrefix + "frame_data.function", method.Name);
            span.Tags.SetTag(tagPrefix + "frame_data.class_name", method.DeclaringType?.Name);
            span.Tags.SetTag(tagPrefix + "snapshot_id", snapshotId);

            ExceptionDebugging.AddSnapshot(probeId, snapshot);
        }

        private static void TagMissingFrame(Span span, string tagPrefix, MethodBase method, string reason)
        {
            span.Tags.SetTag(tagPrefix + "frame_data.function", method.Name);
            span.Tags.SetTag(tagPrefix + "frame_data.class_name", method.DeclaringType?.Name);
            span.Tags.SetTag(tagPrefix + "no_capture_reason", reason);
        }

        private static bool ShouldReportException(Exception ex, ParticipatingFrame[] framesToRejit)
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

            bool AtLeastOneFrameBelongToUserCode() => framesToRejit.All(f => !FrameFilter.IsUserCode(f)) == false;
        }

        private static bool IsSupportedExceptionType(Exception ex) =>
            IsSupportedExceptionType(ex.GetType());

        public static void Initialize()
        {
            _exceptionProcessorTask = Task.Factory.StartNew(
                                               async () => await StartExceptionProcessingAsync(Cts.Token).ConfigureAwait(false), TaskCreationOptions.LongRunning)
                                          .Unwrap();
            IsEditAndContinueFeatureEnabled = IsEnCFeatureEnabled();
            _isInitialized = true;
        }

        /// <summary>
        /// In .NET 6+ there's a bug that prevents Rejit-related APIs to work properly when Edit and Continue feature is turned on.
        /// See https://github.com/dotnet/runtime/issues/91963 for additional details.
        /// </summary>
        private static bool IsEnCFeatureEnabled()
        {
            var encEnabled = EnvironmentHelpers.GetEnvironmentVariable("COMPLUS_ForceEnc");
            return !string.IsNullOrEmpty(encEnabled) && (encEnabled == "1" || encEnabled == "true");
        }

        public static bool IsSupportedExceptionType(Type ex) =>
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

        public static void Dispose()
        {
            Cts.Cancel();
            ReportingCircuitBreaker.Dispose();

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
                WorkAvailable.Dispose();
                Cts.Dispose();
            }
        }

        public static ExceptionRelatedFrames GetAllExceptionRelatedStackFrames(Exception exception)
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
        public static IEnumerable<ParticipatingFrame> GetParticipatingFrames(StackTrace stackTrace, bool isTopFrame, ParticipatingFrameState defaultState)
        {
            var frames = isTopFrame
                             ? stackTrace.GetFrames()?.
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

        private static bool ShouldSkip(MethodBase method)
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

        private static void SetDiagnosticTag(Span span, string exceptionPhase, int exceptionHash)
        {
            span.Tags.SetTag("_dd.di._er", exceptionPhase);
            span.Tags.SetTag("_dd.di._eh", exceptionHash.ToString());
        }
    }
}
