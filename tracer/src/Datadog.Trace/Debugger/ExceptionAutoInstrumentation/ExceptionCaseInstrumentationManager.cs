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
        private static readonly int MaxFramesToCapture = ExceptionDebugging.Settings.MaximumFramesToCapture;

        internal static ExceptionCase Instrument(ExceptionIdentifier exceptionId, string exceptionToString)
        {
            Log.Information("Instrumenting {ExceptionId}", exceptionId);

            var parsedFramesFromExceptionToString = ExceptionNormalizer.Instance.ParseFrames(exceptionToString).ToArray();
            var stackTrace = exceptionId.StackTrace.Where(frame => parsedFramesFromExceptionToString.Any(f => f.Contains(frame.Method.Name))).ToArray();
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

        private static bool ContainsExceptionDispatchInfoThrow(MethodBase method)
        {
            var methodBody = method.GetMethodBody();
            if (methodBody == null)
            {
                return false;
            }

            byte[] ilBytes = methodBody.GetILAsByteArray();

            if (ilBytes == null)
            {
                return false;
            }

            for (int i = 0; i < ilBytes.Length; i++)
            {
                if (ilBytes[i] == (byte)OpCodes.Call.Value || ilBytes[i] == (byte)OpCodes.Callvirt.Value)
                {
                    // The next 4 bytes after a call instruction contain the metadata token
                    if (i + 4 < ilBytes.Length)
                    {
                        int metadataToken = BitConverter.ToInt32(ilBytes, i + 1);
                        try
                        {
                            MethodInfo calledMethod = (MethodInfo)method.Module.ResolveMethod(metadataToken);
                            if (calledMethod.DeclaringType == typeof(System.Runtime.ExceptionServices.ExceptionDispatchInfo) &&
                                calledMethod.Name == "Throw")
                            {
                                return true;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // If we can't resolve the method, just continue
                            continue;
                        }
                    }
                }
            }

            return false;
        }

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
        }
    }
}
