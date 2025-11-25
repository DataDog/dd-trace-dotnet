// <copyright file="ExceptionReplayProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionReplayProbe
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ExceptionReplayProbe>();
        private readonly int _hashCode;
        private readonly object _locker = new();
        private readonly List<ExceptionCase> _exceptionCases = new();
        private int _isInstrumented = 0;
        private int _maxFramesToCapture;

        public ExceptionReplayProbe(MethodUniqueIdentifier method, int maxFramesToCapture)
        {
            Method = method;
            _hashCode = ComputeHashCode();
            _maxFramesToCapture = maxFramesToCapture;
        }

        internal string? ProbeId { get; private set; }

        internal MethodUniqueIdentifier Method { get; }

        internal ExceptionReplayProcessor? ExceptionReplayProcessor { get; private set; }

        internal bool MayBeOmittedFromCallStack { get; private set; }

        internal Status ProbeStatus { get; set; }

        internal string? ErrorMessage { get; set; }

        internal bool IsInstrumented
        {
            get
            {
                return _isInstrumented == 1 && ExceptionReplayProcessor != null && !string.IsNullOrEmpty(ProbeId);
            }
        }

        private bool ShouldInstrument()
        {
            if (Interlocked.CompareExchange(ref _isInstrumented, 1, 0) == 0)
            {
                ProbeId = Guid.NewGuid().ToString();
                ExceptionReplayProcessor = new ExceptionReplayProcessor(ProbeId, Method, _maxFramesToCapture);
                MayBeOmittedFromCallStack = CheckIfMethodMayBeOmittedFromCallStack();

                return true;
            }

            return false;
        }

        /// <summary>
        /// In .NET 6+ there's a bug that prevents Rejit-related APIs to work properly when Edit and Continue feature is turned on.
        /// See https://github.com/dotnet/runtime/issues/91963 for addiitonal details.
        /// </summary>
        private bool CheckIfMethodMayBeOmittedFromCallStack()
        {
            return ExceptionTrackManager.IsEditAndContinueFeatureEnabled &&
                   FrameworkDescription.Instance.IsCoreClr() && RuntimeHelper.IsNetOnward(6) && Method.Method.DeclaringType?.Assembly != null && RuntimeHelper.IsModuleDebugCompiled(Method.Method.DeclaringType.Assembly);
        }

        private void ProcessCase(ExceptionCase @case)
        {
            if (!IsInstrumented)
            {
                return;
            }

            var probes = @case.Probes;

            for (var index = 0; index < probes.Length; index++)
            {
                var probe = probes[index];

                if (probe.Method.Equals(Method))
                {
                    var parentProbes = probes.Take(index).ToArray();
                    var childProbes = probes.Skip(index + 1).ToArray();

                    var processor = new ExceptionProbeProcessor(probe, @case.ExceptionTypes, parentProbes: parentProbes, childProbes: childProbes);
                    @case.Processors.TryAdd(processor, 0);
                    ExceptionReplayProcessor?.AddProbeProcessor(processor);
                }
            }

            foreach (var probe in probes.Where(p => p.IsInstrumented))
            {
                probe.ExceptionReplayProcessor?.InvalidateEnterLeave();
            }
        }

        internal void AddExceptionCase(ExceptionCase @case, bool isPartOfCase)
        {
            var shouldRefreshAfterLock = false;

            lock (_locker)
            {
                if (isPartOfCase && ShouldInstrument())
                {
                    foreach (var existingCase in _exceptionCases)
                    {
                        ProcessCase(existingCase);
                    }

                    if (string.IsNullOrEmpty(ProbeId))
                    {
                        Log.Warning("ProbeId is null in `AddExceptionCase`, with case: {Case}", @case);
                        return;
                    }

                    // We don't care about sampling Exception Probes. To save memory, NopAdaptiveSampler is used.
                    ProbeRateLimiter.Instance.TryAddSampler(ProbeId, NopAdaptiveSampler.Instance);
                    if (!ProbeExpressionsProcessor.Instance.TryAddProbeProcessor(ProbeId, ExceptionReplayProcessor))
                    {
                        Log.Error("Could not add ExceptionReplayProcessor. Method: {TypeName}.{MethodName}", Method.Method.DeclaringType?.Name, Method.Method.Name);
                    }

                    InstrumentationRequester.Instrument(ProbeId!, Method.Method);
                }

                _exceptionCases.Add(@case);
                ProcessCase(@case);

                shouldRefreshAfterLock = @case.Probes?.Length == 1;
            }

            if (shouldRefreshAfterLock)
            {
                TryRefreshSingleFrameProbeStatus();
            }
        }

        internal void RemoveExceptionCase(ExceptionCase @case)
        {
            lock (_locker)
            {
                _exceptionCases.Remove(@case);
            }
        }

        private int ComputeHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Method);
            return hashCode.ToHashCode();
        }

        public bool Equals(ExceptionReplayProbe other)
        {
            return Method.Equals(other.Method);
        }

        public override bool Equals(object? obj)
        {
            return obj is ExceptionReplayProbe other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        /// <summary>
        /// If an exception case only contains a single customer frame, we never build parent/child call-path hashes,
        /// meaning the ordinary probe-status polling code in <see cref="ExceptionProbeProcessor"/> never executes.
        /// For CI Visibility (and other single-frame scenarios) this left probes permanently stuck in the default
        /// <see cref="Status.RECEIVED"/> state, so snapshots were never captured. To avoid changing the behaviour
        /// for multi-frame cases, we perform a one-off eager poll right after the probe is attached. The poll is
        /// executed outside the probe lock because we may wait up to a few seconds while the CLR completes ReJIT and
        /// we do not want to block unrelated instrumentation updates.
        /// </summary>
        private void TryRefreshSingleFrameProbeStatus()
        {
            if (string.IsNullOrEmpty(ProbeId))
            {
                return;
            }

            try
            {
                // In practice the native tracer reports INSTALLED for ~500 ms after we request ReJIT, but CI Visibility
                // tests regularly need a little longer (module load + async offloader). We therefore try a handful of
                // times with a generous delay so we can observe the final INSTRUMENTED status without changing the
                // behaviour for other scenarios.
                const int maxAttempts = 20;
                var stopwatch = Stopwatch.StartNew();

                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var statuses = DebuggerNativeMethods.GetProbesStatuses(new[] { ProbeId });
                    if (statuses.Length == 0)
                    {
                        return;
                    }

                    var previous = ProbeStatus;
                    ProbeStatus = statuses[0].Status;
                    ErrorMessage = statuses[0].ErrorMessage;

                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        var message = $"Eager status refresh for single-frame probe {ProbeId}. Previous={previous}, Current={ProbeStatus}, Attempt={attempt + 1}, ElapsedMs={stopwatch.ElapsedMilliseconds}";
                        Log.Debug("{Message}", message);
                    }

                    if (ProbeStatus == Status.INSTRUMENTED)
                    {
                        break;
                    }

                    if (ProbeStatus == Status.ERROR || ProbeStatus == Status.BLOCKED)
                    {
                        break;
                    }

                    if (attempt < maxAttempts - 1)
                    {
                        Thread.Sleep(attempt == 0 ? 1_500 : 250);
                    }
                }

                if (ProbeStatus != Status.INSTRUMENTED)
                {
                    Log.Warning(
                        "Single-frame probe {ProbeId} never reported INSTRUMENTED during eager refresh. FinalStatus={Status}, TotalWaitMs={ElapsedMs}",
                        ProbeId,
                        ProbeStatus,
                        stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to eagerly refresh probe status for {ProbeId}", ProbeId);
            }
        }
    }
}
