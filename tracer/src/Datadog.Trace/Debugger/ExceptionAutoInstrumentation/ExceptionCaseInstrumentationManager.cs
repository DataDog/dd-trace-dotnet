// <copyright file="ExceptionCaseInstrumentationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
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

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionCaseInstrumentationManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ExceptionCaseInstrumentationManager>();
        private static readonly ConcurrentDictionary<MethodUniqueIdentifier, ExceptionDebuggingProbe> MethodToProbe = new();
        private static readonly int MaxFramesToCapture = ExceptionReplay.Settings.MaximumFramesToCapture;

        internal static ExceptionCase Instrument(ExceptionIdentifier exceptionId, string exceptionToString)
        {
            Log.Information("Instrumenting {ExceptionId}", exceptionId);

            var parsedFramesFromExceptionToString = StackTraceProcessor.ParseFrames(exceptionToString).ToArray();
            var stackTrace = exceptionId.StackTrace.Where(frame => parsedFramesFromExceptionToString.Any(f => MethodMatcher.IsMethodMatch(f, frame.Method))).ToArray();
            var participatingUserMethods = GetMethodsToRejit(stackTrace);

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

            var thresholdIndex = participatingUserMethods.Count - MaxFramesToCapture;
            var targetMethods = new HashSet<MethodUniqueIdentifier>();

            for (var index = 0; index < probes.Length; index++)
            {
                if (ShouldInstrumentFrameAtIndex(index))
                {
                    targetMethods.Add(probes[index].Method);
                }
            }

            var newCase = new ExceptionCase(exceptionId.ExceptionTypes, probes);

            foreach (var method in uniqueMethods)
            {
                var probe = MethodToProbe[method];
                probe.AddExceptionCase(newCase, targetMethods.Contains(method));
            }

            return newCase;

            bool ShouldInstrumentFrameAtIndex(int i)
            {
                return i == 0 || i >= thresholdIndex || participatingUserMethods.Count <= MaxFramesToCapture + 1;
            }
        }

        private static List<MethodUniqueIdentifier> GetMethodsToRejit(ParticipatingFrame[] allFrames)
        {
            var methodsToRejit = new List<MethodUniqueIdentifier>();
            MethodUniqueIdentifier? lastMethod = null;
            var wasLastMisleading = false;

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

                    var currentMethod = frame.MethodIdentifier;
                    var isCurrentMisleading = currentMethod.IsMisleadMethod();

                    // Add the method if either:
                    // 1. It's not misleading (we keep all non-misleading methods)
                    // 2. It's misleading but different from the last misleading method we saw
                    // 3. It's the first misleading method after non-misleading methods
                    if (!isCurrentMisleading || currentMethod != lastMethod || !wasLastMisleading)
                    {
                        methodsToRejit.Add(currentMethod);
                    }

                    lastMethod = currentMethod;
                    wasLastMisleading = isCurrentMisleading;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to instrument the frame: {FrameToRejit}", frame);
                }
            }

            return methodsToRejit;
        }

        internal static void Revert(ExceptionCase @case)
        {
            if (@case.Probes == null || @case.Processors == null)
            {
                Log.Information("Received empty @case, nothing to revert.");
                return;
            }

            try
            {
                Log.Information("Reverting {ExceptionCase}", @case);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to log an exception case while reverting...");
            }

            foreach (var probe in @case.Probes)
            {
                probe.RemoveExceptionCase(@case);
            }

            var revertProbeIds = new HashSet<string>();

            foreach (var processor in @case.Processors.Keys)
            {
                if (processor.ExceptionReplayProcessor.RemoveProbeProcessor(processor) == 0)
                {
                    MethodToProbe.TryRemove(processor.ExceptionReplayProcessor.Method, out _);
                    revertProbeIds.Add(processor.ExceptionReplayProcessor.ProbeId);
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
        }
    }
}
