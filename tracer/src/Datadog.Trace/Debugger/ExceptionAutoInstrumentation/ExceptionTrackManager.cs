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
using System.Xml.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionTrackManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ExceptionTrackManager>();
        private static readonly ConcurrentDictionary<ExceptionIdentifier, TrackedExceptionCase> TrackedExceptionCases = new();
        private static readonly ConcurrentDictionary<MethodUniqueIdentifier, ExceptionDebuggingProbe> MethodToProbe = new();
        private static readonly ConcurrentQueue<Exception> ExceptionProcessQueue = new();
        private static readonly SemaphoreSlim WorkAvailable = new(0, int.MaxValue);
        private static readonly CancellationTokenSource Cts = new();
        private static readonly HashSet<string> CachedDoneExceptions = new();
        private static readonly ReaderWriterLockSlim DoneExceptionsLocker = new();
        private static Task _exceptionProcessorTask;
        private static int _maxFramesToCapture;

        private static async Task StartExceptionProcessingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await WorkAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);

                while (ExceptionProcessQueue.TryDequeue(out var exception))
                {
                    ProcessException(exception, ErrorOriginType.HttpRequestFailure, rootSpan: null);
                }
            }
        }

        public static void Report(Span span, Exception exception)
        {
            // For V1 of Exception Debugging, we only care about exceptions propagating up the stack
            // and marked as error by the service entry/root span.
            if (!span.IsRootSpan || exception == null || !IsSupportedExceptionType(exception))
            {
                Log.Information(exception, "Skipping the processing of the exception. Span = {Span}", span);
                return;
            }

            try
            {
                ReportInternal(exception, ErrorOriginType.HttpRequestFailure, span);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An exception was thrown while processing an exception for tracking.");
            }
        }

        private static void ReportInternal(Exception exception, ErrorOriginType errorOrigin, Span rootSpan)
        {
            if (IsInDoneState(exception))
            {
                // Quick exit.
                return;
            }

            var nonEmptyShadowStack = ShadowStackContainer.IsShadowStackTrackingEnabled && ShadowStackContainer.ShadowStack.ContainsReport(exception);
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

        /// <summary>
        /// Quick lookup for early exit.
        /// </summary>
        private static bool IsInDoneState(Exception exception)
        {
            DoneExceptionsLocker.EnterReadLock();
            try
            {
                return CachedDoneExceptions.Contains(exception.ToString());
            }
            finally
            {
                DoneExceptionsLocker.ExitReadLock();
            }
        }

        private static void ProcessException(Exception exception, ErrorOriginType errorOrigin, Span rootSpan)
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

            var trackedExceptionCase = TrackedExceptionCases.GetOrAdd(exceptionId, _ => new TrackedExceptionCase(exceptionId, TimeSpan.FromSeconds(1)));

            if (trackedExceptionCase.IsDone)
            {
                DoneExceptionsLocker.EnterWriteLock();
                try
                {
                    CachedDoneExceptions.Add(exception.ToString());
                }
                finally
                {
                    DoneExceptionsLocker.ExitWriteLock();
                }
            }
            else if (trackedExceptionCase.IsCollecting)
            {
                Log.Information("Exception case re-occurred, data can be collected. Exception details: {FullName} {Message}.", exception.GetType().FullName, exception.Message);

                if (rootSpan == null)
                {
                    Log.Information("The RootSpan is null. The exception might be reported from async processing. Should not happen. Exception: {Exception}", exception.ToString());
                    return;
                }

                if (!ShadowStackContainer.IsShadowStackTrackingEnabled)
                {
                    Log.Warning("The shadow stack is not enabled, while processing IsCollecting state of an exception. Exception details: {FullName} {Message}.", exception.GetType().FullName, exception.Message);
                    return;
                }

                var resultCallStackTree = ShadowStackContainer.ShadowStack.CreateResultReport(exceptionPath: exception);
                if (resultCallStackTree == null || !resultCallStackTree.Frames.Any())
                {
                    Log.Error("ExceptionTrackManager: Received an empty tree from the shadow stack for exception: {Exception}.", exception.ToString());
                    System.Diagnostics.Debugger.Break();
                }
                else
                {
                    // TODO Ensure all snapshots present. I.e we are not in partial capturing scenario.

                    // Attach tags to the root span
                    var debugErrorPrefix = "_dd.debug.error";
                    rootSpan.Tags.SetTag("error.debug_info_captured", "true");
                    rootSpan.Tags.SetTag($"{debugErrorPrefix}.exception_id", trackedExceptionCase.ErrorHash);

                    var @case = trackedExceptionCase.ExceptionCase;
                    var capturedFrames = resultCallStackTree.Frames;
                    var allFrames = @case.ExceptionId.StackTrace;

                    var frameIndex = allFrames.Length - 1;
                    var capturedFrameIndex = capturedFrames.Count - 1;
                    var assignIndex = 0;

                    // Upload tail frames
                    while (frameIndex >= 0 && capturedFrameIndex >= 0 && assignIndex < _maxFramesToCapture)
                    {
                        var frame = capturedFrames[capturedFrameIndex];

                        if (!allFrames[frameIndex--].Method.Equals(frame.MethodInfo.Method))
                        {
                            continue;
                        }

                        capturedFrameIndex--;

                        var prefix = $"{debugErrorPrefix}.{assignIndex++}.";
                        TagAndUpload(rootSpan, prefix, frame);
                    }

                    // Upload head frame
                    if (capturedFrames.Count > _maxFramesToCapture)
                    {
                        var frame = capturedFrames[0];

                        frameIndex = 0;

                        while (frameIndex < allFrames.Length &&
                               !allFrames[frameIndex].Method.Equals(frame.MethodInfo.Method))
                        {
                            frameIndex++;
                        }

                        var prefix = $"{debugErrorPrefix}.{allFrames.Length - frameIndex - 1}.";
                        TagAndUpload(rootSpan, prefix, frame);
                    }

                    if (trackedExceptionCase.BeginTeardown())
                    {
                        foreach (var probe in trackedExceptionCase.ExceptionCase.Probes)
                        {
                            probe.RemoveExceptionCase(trackedExceptionCase.ExceptionCase);
                        }

                        var revertProbeIds = new HashSet<string>();

                        foreach (var processor in trackedExceptionCase.ExceptionCase.Processors.Keys)
                        {
                            if (processor.ExceptionDebuggingProcessor.RemoveProbeProcessor(processor) == 0)
                            {
                                MethodToProbe.TryRemove(processor.ExceptionDebuggingProcessor.Method, out _);
                                revertProbeIds.Add(processor.ExceptionDebuggingProcessor.ProbeId);
                            }
                        }

                        if (revertProbeIds.Count > 0)
                        {
                            Log.Information("ExceptionTrackManager: Reverting {RevertCount} Probes.", revertProbeIds.Count.ToString());

                            var removeProbesRequests = revertProbeIds.Select(p => new NativeRemoveProbeRequest(p)).ToArray();
                            DebuggerNativeMethods.InstrumentProbes(
                                Array.Empty<NativeMethodProbeDefinition>(),
                                Array.Empty<NativeLineProbeDefinition>(),
                                Array.Empty<NativeSpanProbeDefinition>(),
                                removeProbesRequests);
                        }

                        trackedExceptionCase.EndTeardown();
                    }
                }
            }
            else
            {
                // else - If there is a concurrent initialization or tearing down, ignore this case
                if (!trackedExceptionCase.Initialized())
                {
                    return;
                }

                Log.Information("New exception case occurred, initiating data collection for exception: {Name}, Message: {Message}, StackTrace: {StackTrace}", exception.GetType().Name, exception.Message, exception.StackTrace);

                trackedExceptionCase.ExceptionCase = InstrumentFrames(trackedExceptionCase.ExceptionIdentifier);

                trackedExceptionCase.BeginCollect();
            }
        }

        private static void TagAndUpload(Span span, string tagPrefix, ExceptionStackNodeRecord record)
        {
            var method = record.MethodInfo.Method;
            var snapshotId = record.SnapshotId;
            var snapshot = record.Snapshot;
            var probeId = record.ProbeId;

            span.Tags.SetTag(tagPrefix + "frame_data.function", method.Name);
            span.Tags.SetTag(tagPrefix + "frame_data.class_name", method.DeclaringType.Name);
            span.Tags.SetTag(tagPrefix + "snapshot_id", snapshotId);

            LiveDebugger.Instance.AddSnapshot(probeId, snapshot);
        }

        private static bool ShouldReportException(Exception ex, ParticipatingFrame[] framesToRejit)
        {
            try
            {
                // Both OutOfMemoryException and ThreadAbortException may be thrown anywhere from non-deterministic reasons, which means it won't
                // necessarily re-occur with the same callstack. We should not attempt to rejit methods nor track these exceptions.

                // ThreadAbortException is particularly problematic because is it often thrown in legacy ASP.NET apps whenever
                // there is an HTTP Redirect. See also https://stackoverflow.com/questions/2777105/why-response-redirect-causes-system-threading-threadabortexception
                if (ex is OutOfMemoryException || ex is ThreadAbortException)
                {
                    return false;
                }

                return AtLeastOneFrameBelongToUserCode() && ThereIsNoFrameThatBelongsToDatadogClrProfilerAgentCode();
            }
            catch
            {
                // When in doubt, report it.
                return true;
            }

            bool AtLeastOneFrameBelongToUserCode() => framesToRejit.All(f => !FrameFilter.IsUserCode(f)) == false;
            bool ThereIsNoFrameThatBelongsToDatadogClrProfilerAgentCode() => framesToRejit.Any(f => FrameFilter.IsDatadogAssembly(f.Method.Module.Assembly.GetName().Name)) == false;
        }

        private static bool IsSupportedExceptionType(Exception ex) =>
            IsSupportedExceptionType(ex?.GetType());

        public static void Initialize()
        {
            _maxFramesToCapture = ExceptionDebugging.Settings.MaximumFramesToCapture;

            _exceptionProcessorTask = Task.Factory.StartNew(
                                               async () => await StartExceptionProcessingAsync(Cts.Token).ConfigureAwait(false), TaskCreationOptions.LongRunning)
                                          .Unwrap();

            LifetimeManager.Instance.AddShutdownTask(Stop);
        }

        public static bool IsSupportedExceptionType(Type ex) =>
            ex != typeof(BadImageFormatException) &&
            ex != typeof(InvalidProgramException) &&
            ex != typeof(TypeInitializationException) &&
            ex != typeof(TypeLoadException) &&
            ex != typeof(OutOfMemoryException);

        private static List<MethodUniqueIdentifier> GetMethodsToRejit(ParticipatingFrame[] allFrames)
        {
            var methodsToRejit = new List<MethodUniqueIdentifier>();

            foreach (var frame in allFrames)
            {
                try
                {
                    // HasMethod?

                    if (frame.State == ParticipatingFrameState.Blacklist)
                    {
                        continue;
                    }

                    var frameMethod = frame.Method;
                    if (frameMethod.IsAbstract)
                    {
                        continue;
                    }

                    methodsToRejit.Add(frame.MethodIdentifier);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to instrument frame the frame: {FrameToRejit}", frame);
                }
            }

            return methodsToRejit;
        }

        private static ExceptionCase InstrumentFrames(ExceptionIdentifier exceptionIdentifier)
        {
            var participatingUserMethods = GetMethodsToRejit(exceptionIdentifier.StackTrace);

            var uniqueMethods = participatingUserMethods
                               .Distinct(EqualityComparer<MethodUniqueIdentifier>.Default)
                               .ToArray();

            var neverSeenBeforeMethods = uniqueMethods
                                        .Where(frame => !MethodToProbe.ContainsKey(frame))
                                        .ToArray();

            foreach (var frame in neverSeenBeforeMethods)
            {
                MethodToProbe.TryAdd(frame, new ExceptionDebuggingProbe(frame));
            }

            var probes = participatingUserMethods.Select((m, frameIndex) => MethodToProbe[m]).ToArray();

            var thresholdIndex = participatingUserMethods.Count - _maxFramesToCapture;
            var targetMethods = new HashSet<MethodUniqueIdentifier>();

            for (var index = 0; index < probes.Length; index++)
            {
                if (ShouldInstrumentFrameAtIndex(index))
                {
                    targetMethods.Add(probes[index].Method);
                }
            }

            var newCase = new ExceptionCase(exceptionIdentifier, probes);

            foreach (var method in uniqueMethods)
            {
                var probe = MethodToProbe[method];
                probe.AddExceptionCase(newCase, targetMethods.Contains(method));
            }

            // TODO decide if a sampler is needed, ExceptionProbeProcessor does not use any sampler for now.
            // TODO InnerExceptions poses struggle in ExceptionProbeProcessor leaving logic.
            // TODO Capture arguments on exit upon first leave, collect lightweight snapshot for subsequent re-entrances.
            // TODO AsyncLocal cleansing when done dealing with exception from the Exception Debugging instrumentation (ShadowStack cleansing)
            // TODO In ExceptionProbeProcessor.ShouldProcess, maybe negotiate with the ShadowStack to determine if the top of the stack
            // TODO     is relevant for the specific exception case it manages. Maybe instead of ShouldProcess we can do that
            // TODO     in the Process method, in the branch where the exception type is checked to see if the previous method is relevant.
            // TODO     there's a gotcha in doing it - it might be the next method has not been instrumented (failed to instrument)
            // TODO     so it won't be there because it should. We will have to accommodate for that by checking the probe status and cache it.
            // TODO When leaving with an exception, we can negotiate with the ShadowStack to determine if the previous frame
            // TODO     Is holding the same exception instance (either as inner / itself) to better decide if we should keep on collecting
            // TODO     or not.
            // TODO Multiple AppDomains issue. The ProbeProcessor might not be there. Also relevant for DI probes. To assess how big
            // TODO     the issue is, we should determine how many people are using .NET Framework .VS. .NET Core.
            // TODO     For Exception Debugging we can possibly choose to ditch this altogether since if the same exception will
            // TODO     happen multiple times in different AppDomains, then they will all capture the exception. The only problem is
            // TODO     over-instrumenting which is not ideal.
            // TODO In AsyncMethodProbe Invoker, is it always MultiProbe even when there is only one?
            // TODO What do you do with empty shadow stack? meaning, all the participating methods has failed in the instrumentation process OR they are all 3rd party code?
            // TODO There might be two different exceptions, that yield the same snapshots. Consider A -> B -> C with exception "InvalidOperationException"
            // TODO     and K -> B -> D with exception "InvalidOperationException". If we fail to instrument: A, B, K, D then there will be the same causality chain for both exceptions.
            // TODO     That's why ExceptionTrackManager is the only place where snapshots are uploaded, based on the exception in hand, to be able to stop tracking an exception
            // TODO     and keep on tracking the other.
            // TODO For Lightweight/Full snapshot capturing:
            // TODO     Consider keeping a cache in ShadowStackTree's AsyncLocal (in ShadowStackContainer), where the cached key
            // TODO     will be the hash of parents & children (Enter/Leave) and the MethodToken of the method. This way,
            // TODO     the method that is leaving with an interesting exception can ask this AsyncLocal (top-thread-tree) cache
            // TODO     if it's hash (EnterHash+LeaveHash+MethodToken) is in there. If it is, collect lightweight snapshot.
            // TODO     if it's not, collect full snapshot.
            // TODO     In this technique we will have to verify AsyncLocal safety in terms of memory leaking and the cleansing timing.
            // TODO     we don't want this cache to be alive for a longer time than is needed or being reused by another execution
            // TODO     context in a later time. This cache will have to be thread-safe since many threads may access it at the same
            // TODO     time. Consider using Readers/Writer lock pattern or another one that is prioritizing readings than writings.
            // TODO     Or any other lock-free pattern that may be suitable in this case.
            // TODO Better handle multiple exceptions related to concurrency - AggregateException. It's InnerException &
            // TODO     InnerExceptions properties.

            return newCase;

            bool ShouldInstrumentFrameAtIndex(int i)
            {
                return i == 0 || i >= thresholdIndex || participatingUserMethods.Count <= _maxFramesToCapture + 1;
            }
        }

        private static void Stop()
        {
            Cts.Cancel();

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

                if (FrameFilter.ShouldSkip(method))
                {
                    yield return new ParticipatingFrame(frame, ParticipatingFrameState.Blacklist);
                }
                else
                {
                    yield return new ParticipatingFrame(frame, defaultState);
                }
            }
        }
    }
}
