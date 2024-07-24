// <copyright file="ExceptionProbeProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using Fnv1aHash = Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal.Hash;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionProbeProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExceptionProbeProcessor));
        private readonly HashSet<Type> _exceptionTypes;
        private readonly Type? _singleExceptionType;
        private readonly ExceptionDebuggingProbe[] _childProbes;
        private readonly ExceptionDebuggingProbe[] _parentProbes;
        private readonly object _locker = new();
        private int? _enterSequenceHash;
        private int? _leaveSequenceHash;

        internal ExceptionProbeProcessor(ExceptionDebuggingProbe probe, HashSet<Type> exceptionTypes, ExceptionDebuggingProbe[] parentProbes, ExceptionDebuggingProbe[] childProbes)
        {
            ExceptionDebuggingProcessor = probe.ExceptionDebuggingProcessor ?? throw new ArgumentException($"probe.ExceptionDebuggingProcessor is null. Probe: {probe}");
            _exceptionTypes = exceptionTypes;
            _singleExceptionType = _exceptionTypes.Count == 1 ? _exceptionTypes.Single() : null;
            _childProbes = childProbes;
            _parentProbes = parentProbes;
        }

        internal ExceptionDebuggingProcessor ExceptionDebuggingProcessor { get; }

        internal bool ShouldProcess(int enterSequenceHash)
        {
            if (!EnsureEnterHashComputed())
            {
                return false;
            }

            return enterSequenceHash == _enterSequenceHash;
        }

        private bool EnsureEnterHashComputed()
        {
            if (_enterSequenceHash == null)
            {
                lock (_locker)
                {
                    _enterSequenceHash ??= ComputeSequenceHash(_parentProbes);
                }
            }

            return _enterSequenceHash != null;
        }

        private bool EnsureLeaveHashComputed()
        {
            if (_leaveSequenceHash == null)
            {
                lock (_locker)
                {
                    _leaveSequenceHash ??= ComputeSequenceHash(_childProbes, reverseOrder: true);
                }
            }

            return _leaveSequenceHash != null;
        }

        private int? ComputeSequenceHash(ExceptionDebuggingProbe[] probes, bool reverseOrder = false)
        {
            // Ensure we have instrumented probes to work with
            var instrumentedProbes = probes.Where(p => p.IsInstrumented).ToArray();

            var probeIds = instrumentedProbes
                          .Where(p => p.ProbeStatus == Status.RECEIVED || (p.ProbeStatus == Status.INSTALLED && !p.MayBeOmittedFromCallStack))
                          .Select(p => p.ProbeId)
                          .ToArray();

            var statuses = DebuggerNativeMethods.GetProbesStatuses(probeIds);

            foreach (var status in statuses)
            {
                var probe = instrumentedProbes.FirstOrDefault(p => p.ProbeId == status.ProbeId);
                if (probe != null && status.Status is Status.INSTALLED or Status.INSTRUMENTED or Status.ERROR)
                {
                    probe.ProbeStatus = status.Status;
                    probe.ErrorMessage = status.ErrorMessage;
                }
            }

            if (instrumentedProbes.Any(p => !p.MayBeOmittedFromCallStack && p.ProbeStatus == Status.INSTALLED))
            {
                // Not all request rejit carried out yet.
                return null;
            }

            var installedProbes = instrumentedProbes
                                    .Where(p => p.ProbeStatus is Status.INSTRUMENTED)
                                    .ToArray();

            if (reverseOrder)
            {
                Array.Reverse(installedProbes);
            }

            if (!installedProbes.Any())
            {
                return Fnv1aHash.FnvOffsetBias;
            }

            var hash = Fnv1aHash.FnvOffsetBias;

            foreach (var probe in installedProbes)
            {
                hash = Fnv1aHash.Combine(probe.Method.MethodToken, hash);
            }

            return hash;
        }

        internal void InvalidateEnterLeave()
        {
            lock (_locker)
            {
                _enterSequenceHash = null;
                _leaveSequenceHash = null;
            }
        }

        internal void Enter<TCapture>(ref CaptureInfo<TCapture> info, ExceptionSnapshotCreator snapshotCreator, in ProbeData probeData)
        {
        }

        internal bool Leave(Type exceptionType, ExceptionSnapshotCreator snapshotCreator)
        {
            if (!EnsureEnterHashComputed() || _enterSequenceHash != snapshotCreator.EnterHash)
            {
                return false;
            }

            if (_singleExceptionType == exceptionType || _exceptionTypes.Contains(exceptionType))
            {
                if (!EnsureLeaveHashComputed() || _leaveSequenceHash != snapshotCreator.LeaveHash)
                {
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
