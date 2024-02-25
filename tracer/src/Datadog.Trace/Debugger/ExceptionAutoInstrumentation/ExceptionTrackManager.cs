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
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.Vendors.Serilog.Events;
using ProbeInfo = Datadog.Trace.Debugger.Expressions.ProbeInfo;
using ProbeLocation = Datadog.Trace.Debugger.Expressions.ProbeLocation;

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
        private static readonly int MaxFramesToCapture = ExceptionDebugging.Settings.MaximumFramesToCapture;
        private static readonly TimeSpan RateLimit = ExceptionDebugging.Settings.RateLimit;
        private static readonly BasicCircuitBreaker ReportingCircuitBreaker = new(ExceptionDebugging.Settings.MaxExceptionAnalysisLimit, TimeSpan.FromSeconds(1));
        private static Task _exceptionProcessorTask;
        private static bool _isInitialized;

        private static async Task StartExceptionProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await WorkAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);

                while (ExceptionProcessQueue.TryDequeue(out var exception))
                {
                    ProcessException(exception, ErrorOriginKind.HttpRequestFailure, rootSpan: null);
                }
            }
        }

        public static void Report(Span span, Exception exception)
        {
            if (!_isInitialized)
            {
                Log.Information(exception, "Exception Track Manager is not initialized yet. Skipping the processing of an exception. Span = {Span}", span);
                return;
            }

            // For V1 of Exception Debugging, we only care about exceptions propagating up the stack
            // and marked as error by the service entry/root span.
            if (!span.IsRootSpan || exception == null || !IsSupportedExceptionType(exception))
            {
                Log.Information(exception, "Skipping the processing of the exception. Span = {Span}", span);
                return;
            }

            try
            {
                ReportInternal(exception, ErrorOriginKind.HttpRequestFailure, span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An exception was thrown while processing an exception for tracking.");
            }
        }

        private static void ReportInternal(Exception exception, ErrorOriginKind errorOrigin, Span rootSpan)
        {
            if (CachedDoneExceptions.Contains(exception.ToString()))
            {
                // Quick exit.
                return;
            }

            if (ReportingCircuitBreaker.Trip() == CircuitBreakerState.Opened)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug(exception, "The circuit breaker is opened, skipping the processing of an exception.");
                }

                return;
            }

            var nonEmptyShadowStack = ShadowStackHolder.IsShadowStackTrackingEnabled && ShadowStackHolder.ShadowStack.ContainsReport(exception);
            if (nonEmptyShadowStack)
            {
                ProcessException(exception, errorOrigin, rootSpan);
            }
            else
            {
                ExceptionProcessQueue.Enqueue(exception);
                WorkAvailable.Release();
            }
        }

        private static void ProcessException(Exception exception, ErrorOriginKind errorOrigin, Span rootSpan)
        {
            var allParticipatingFrames = GetAllExceptionRelatedStackFrames(exception);
            var allParticipatingFramesFlattened = allParticipatingFrames.GetAllFlattenedFrames().Reverse().ToArray();

            if (allParticipatingFramesFlattened.Length == 0)
            {
                return;
            }

            if (!ShouldReportException(exception, allParticipatingFramesFlattened))
            {
                Log.Information(exception, "Skipping the processing of an exception by Exception Debugging.");
                return;
            }

            var exceptionTypes = new HashSet<Type>();
            var currentFrame = allParticipatingFrames;

            while (currentFrame != null)
            {
                exceptionTypes.Add(currentFrame.Exception.GetType());
                currentFrame = currentFrame.InnerFrame;
            }

            var exceptionId = new ExceptionIdentifier(exceptionTypes, allParticipatingFramesFlattened, errorOrigin);

            var trackedExceptionCase = TrackedExceptionCases.GetOrAdd(exceptionId, _ => new TrackedExceptionCase(exceptionId, exception.ToString()));

            if (trackedExceptionCase.IsDone)
            {
            }
            else if (trackedExceptionCase.IsCollecting)
            {
                Log.Information("Exception case re-occurred, data can be collected. Exception details: {FullName} {Message}.", exception.GetType().FullName, exception.Message);

                if (rootSpan == null)
                {
                    Log.Information("The RootSpan is null. The exception might be reported from async processing. Should not happen. Exception: {Exception}", exception.ToString());
                    return;
                }

                if (!ShadowStackHolder.IsShadowStackTrackingEnabled)
                {
                    Log.Warning("The shadow stack is not enabled, while processing IsCollecting state of an exception. Exception details: {FullName} {Message}.", exception.GetType().FullName, exception.Message);
                    return;
                }

                var resultCallStackTree = ShadowStackHolder.ShadowStack.CreateResultReport(exceptionPath: exception);
                if (resultCallStackTree == null || !resultCallStackTree.Frames.Any())
                {
                    Log.Error("ExceptionTrackManager: Received an empty tree from the shadow stack for exception: {Exception}.", exception.ToString());

                    // If we failed to instrument all the probes.
                    if (trackedExceptionCase.ExceptionCase.Probes.All(p => p.IsInstrumented && (p.ProbeStatus == Status.ERROR || p.ProbeStatus == Status.BLOCKED || (p.ProbeStatus == Status.INSTALLED && p.MayBeOmittedFromCallStack))))
                    {
                        var allFrames = trackedExceptionCase.ExceptionCase.ExceptionId.StackTrace;
                        var allProbes = trackedExceptionCase.ExceptionCase.Probes;
                        var frameIndex = allFrames.Length - 1;
                        var debugErrorPrefix = "_dd.debug.error";
                        var assignIndex = 0;

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

                        Log.Information("Reverting the exception case of the empty stack tree since none of the methods were instrumented, for exception: {Name}, Message: {Message}, StackTrace: {StackTrace}", exception.GetType().Name, exception.Message, exception.StackTrace);
                        if (trackedExceptionCase.Revert())
                        {
                            CachedDoneExceptions.Add(trackedExceptionCase.ExceptionToString);
                        }
                    }
                }
                else
                {
                    // Attach tags to the root span
                    var debugErrorPrefix = "_dd.debug.error";
                    rootSpan.Tags.SetTag("error.debug_info_captured", "true");
                    rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_id", trackedExceptionCase.ErrorHash);

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

                        if (!allFrames[frameIndex--].Method.Equals(frame.MethodInfo.Method))
                        {
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

                    if (trackedExceptionCase.Revert())
                    {
                        CachedDoneExceptions.Add(trackedExceptionCase.ExceptionToString);
                        ExceptionsScheduler.Schedule(trackedExceptionCase, RateLimit);
                    }
                }
            }
            else
            {
                Log.Information("New exception case occurred, initiating data collection for exception: {Name}, Message: {Message}, StackTrace: {StackTrace}", exception.GetType().Name, exception.Message, exception.StackTrace);
                trackedExceptionCase.Instrument();
            }
        }

        private static string GetNoCaptureReason(ParticipatingFrame frame, ExceptionDebuggingProbe probe)
        {
            var noCaptureReason = GetNoCaptureReasonForFrame(frame);

            if (noCaptureReason != string.Empty)
            {
                return noCaptureReason;
            }

            if (probe != null)
            {
                if (probe.ProbeStatus == Status.INSTALLED && probe.MayBeOmittedFromCallStack)
                {
                    // Frame is non-optimized in .NET 6+.
                    noCaptureReason = $"The method {frame.Method.GetFullyQualifiedName()} could not be captured.";
                }

                if (probe.ProbeStatus == Status.ERROR)
                {
                    // Frame is failed to instrument.
                    noCaptureReason = $"The method {frame.Method.GetFullyQualifiedName()} has failed in instrumentation. Failure reason: {probe.ErrorMessage}";
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
            span.Tags.SetTag(tagPrefix + "frame_data.class_name", method.DeclaringType.Name);
            span.Tags.SetTag(tagPrefix + "snapshot_id", snapshotId);

            tagPrefix = tagPrefix.Replace("_", string.Empty);

            span.Tags.SetTag(tagPrefix + "frame_data.function", method.Name);
            span.Tags.SetTag(tagPrefix + "frame_data.class_name", method.DeclaringType.Name);
            span.Tags.SetTag(tagPrefix + "snapshot_id", snapshotId);

            LiveDebugger.Instance.AddSnapshot(probeId, snapshot);
        }

        private static void TagMissingFrame(Span span, string tagPrefix, MethodBase method, string reason)
        {
            span.Tags.SetTag(tagPrefix + "frame_data.function", method.Name);
            span.Tags.SetTag(tagPrefix + "frame_data.class_name", method.DeclaringType.Name);
            span.Tags.SetTag(tagPrefix + "no_capture_reason", reason);

            tagPrefix = tagPrefix.Replace("_", string.Empty);

            span.Tags.SetTag(tagPrefix + "frame_data.function", method.Name);
            span.Tags.SetTag(tagPrefix + "frame_data.class_name", method.DeclaringType.Name);
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
            IsSupportedExceptionType(ex?.GetType());

        public static void Initialize()
        {
            _exceptionProcessorTask = Task.Factory.StartNew(
                                               async () => await StartExceptionProcessingAsync(Cts.Token).ConfigureAwait(false), TaskCreationOptions.LongRunning)
                                          .Unwrap();

            LifetimeManager.Instance.AddShutdownTask(Stop);

            _isInitialized = true;
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

        private static void Stop()
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
            if (exception == null)
            {
                return ExceptionRelatedFrames.Empty;
            }

            return CreateExceptionPath(exception, true);

            ExceptionRelatedFrames CreateExceptionPath(Exception e, bool isTopFrame)
            {
                var frames = GetParticipatingFrames(new StackTrace(e, false), isTopFrame, ParticipatingFrameState.Default);

                ExceptionRelatedFrames innerFrame = null;

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
                                          SkipWhile(frame => FrameFilter.ShouldSkipNamespaceIfOnTopOfStack(frame.GetMethod())).
                                          Reverse().
                                          GetAsyncFriendlyFrameMethods()
                             : stackTrace.GetFrames()?.GetAsyncFriendlyFrameMethods();

            if (frames == null)
            {
                yield break;
            }

            foreach (var frame in frames)
            {
                var method = frame?.GetMethod();

                if (method == null)
                {
                    continue;
                }

                var assembly = method.Module.Assembly;
                var assemblyName = assembly.GetName().Name;

                if (FrameFilter.IsDatadogAssembly(assemblyName))
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
    }
}
