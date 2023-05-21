// <copyright file="ExceptionTrackManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.Sink.Models;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class ExceptionTrackManager
    {
        private static readonly ConcurrentDictionary<ExceptionIdentifier, ExceptionCase> ExceptionCases = new();

        public static void Notify(Exception exception)
        {
            try
            {
                InnerNotify(exception);
            }
            catch
            {
                // TODO Log
            }
            finally
            {
                SafeCleanseAsyncLocals();
            }
        }

        private static void SafeCleanseAsyncLocals()
        {
            try
            {
                // TODO Cleaning
            }
            catch
            {
                // TODO Log
            }
        }

        private static void InnerNotify(Exception exception)
        {
            var frames = GetFilteredFrames(exception).ToArray();
            var exceptionIdentifier = new
                ExceptionIdentifier(exception.GetType().FullName, frames);

            if (!ExceptionCases.TryGetValue(exceptionIdentifier, out var exceptionCase))
            {
                ExceptionCases.GetOrAdd(exceptionIdentifier, CreateNewExceptionCase);
            }
            else
            {
                var span = Tracer.Instance.InternalActiveScope.Span;
                var debuggerSnapshots = GetActiveDebuggerSnapshots(exception, span);

                if (debuggerSnapshots.Length == 0)
                {
                    return;
                }

                var probeIds = exceptionCase.Probes.Select(p => p.ProbeId).ToArray();
                var statuses = DebuggerNativeMethods.GetProbesStatuses(probeIds); // Expensive, maybe save in a cache & create circuit-breaker on top
                if (statuses.All(status => status.Status is Status.INSTALLED or Status.ERROR) &&
                    statuses.Any(status => status.Status == Status.INSTALLED))
                {
                    // Check against the shadow stack (aka snapshot stack)
                    // Be mindful to recursion calls by deducting them.
                    // Check debuggerSnapshots against the ProbeStatus[] received from the native side (or exceptionCase.ProbeIds?)
                    var capturedExceptionFullCausalityChain = true;
                    var debuggerSnapshotIndex = 0;
                    foreach (var currentFrame in exceptionIdentifier.StackTrace)
                    {
                        var probeId = exceptionCase.Probes.Single(probe => probe.Frame == currentFrame).ProbeId;
                        var probeStatus = statuses.Single(status => status.ProbeId == probeId).Status;

                        if (probeStatus == Status.ERROR)
                        {
                            continue;
                        }

                        if (debuggerSnapshotIndex >= debuggerSnapshots.Length)
                        {
                            capturedExceptionFullCausalityChain = false;
                            break;
                        }

                        var snapshot = debuggerSnapshots[debuggerSnapshotIndex];

                        // if (snapshot.ProbeId != probeId)
                        // {
                        //     capturedExceptionFullCausalityChain = false;
                        //     break;
                        // }

                        string prefix = $"error.debug.{debuggerSnapshotIndex}.";
                        span.Tags.SetTag(prefix + "frame_data.function", currentFrame.MethodName);
                        span.Tags.SetTag(prefix + "frame_data.class_name", currentFrame.TypeName);
                        span.Tags.SetTag(prefix + "snapshot.id", snapshot.SnapshotId.ToString());

                        debuggerSnapshotIndex++;
                    }

                    if (!capturedExceptionFullCausalityChain)
                    {
                        // TODO Log?
                        return;
                    }

                    // We have everything we need!
                    // TODO Upload snapshot(s)

                    foreach (var debuggerSnapshot in debuggerSnapshots)
                    {
                        LiveDebugger.Instance.AddSnapshot(debuggerSnapshot.ProbeId, debuggerSnapshot.Snapshot);
                    }

                    System.Diagnostics.Debugger.Break();
                }
            }
        }

        private static DebuggerSnapshot[] GetActiveDebuggerSnapshots(Exception exception, Span span)
        {
            var snapshots = span.Snapshots;

            if (snapshots.Count == 0)
            {
                return Array.Empty<DebuggerSnapshot>();
            }

            return snapshots.Where(snapshot => snapshot.ExceptionThrown == exception).ToArray();
        }

        private static ExceptionCase CreateNewExceptionCase(ExceptionIdentifier exceptionIdentifier)
        {
            var normalizedFrames = exceptionIdentifier.StackTrace
                                                      .Distinct(EqualityComparer<FrameIdentifier>.Default)
                                                      .ToArray();
            var methodProbeDefinitions = normalizedFrames
                                        .Select(CreateMethodProbe)
                                        .ToArray();

            DebuggerNativeMethods.InstrumentProbes(
                methodProbeDefinitions,
                Array.Empty<NativeLineProbeDefinition>(),
                Array.Empty<NativeSpanProbeDefinition>(),
                Array.Empty<NativeRemoveProbeRequest>());

            var probes = normalizedFrames.Select((t, frameIndex) => new Probe(t, methodProbeDefinitions[frameIndex].ProbeId)).ToArray();
            foreach (var probe in probes)
            {
                Tracer.RegisterFakeProbeProcessor($"{probe.Frame.TypeName}.{probe.Frame.MethodName} - exception", probe.ProbeId, new Where() { TypeName = probe.Frame.TypeName, MethodName = probe.Frame.MethodName });
            }

            return new ExceptionCase(probes);
        }

        private static NativeMethodProbeDefinition CreateMethodProbe(FrameIdentifier frame)
        {
            return new NativeMethodProbeDefinition(Guid.NewGuid().ToString(), frame.TypeName, frame.MethodName, targetParameterTypesFullName: null);
        }

        private static IEnumerable<FrameIdentifier> GetFilteredFrames(Exception exception)
        {
            var frames = new System.Diagnostics.StackTrace(exception);

            foreach (var frame in frames.GetFrames())
            {
                var method = frame.GetMethod();
                if (method?.DeclaringType == null)
                {
                    continue;
                }

                if (IsUserCode(method.DeclaringType.FullName))
                {
                    yield return new FrameIdentifier(method.DeclaringType.FullName, method.Name);
                }
            }
        }

        private static bool IsUserCode(string methodFullName) // Not Real
        {
            if (methodFullName.Contains("eShopWeb"))
            {
                return true;
            }

            return !(methodFullName.StartsWith("Microsoft") ||
                     methodFullName.StartsWith("System") ||
                     methodFullName.StartsWith("Datadog.Trace"));
        }
    }
}
