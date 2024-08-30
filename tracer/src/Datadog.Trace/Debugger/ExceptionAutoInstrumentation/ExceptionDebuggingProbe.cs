// <copyright file="ExceptionDebuggingProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionDebuggingProbe
    {
        private readonly int _hashCode;
        private readonly object _locker = new();
        private readonly List<ExceptionCase> _exceptionCases = new();
        private int _isInstrumented = 0;

        public ExceptionDebuggingProbe(MethodUniqueIdentifier method)
        {
            Method = method;
            _hashCode = ComputeHashCode();
        }

        internal string? ProbeId { get; private set; }

        internal MethodUniqueIdentifier Method { get; }

        internal ExceptionDebuggingProcessor? ExceptionDebuggingProcessor { get; private set; }

        internal bool MayBeOmittedFromCallStack { get; private set; }

        internal Status ProbeStatus { get; set; }

        internal string? ErrorMessage { get; set; }

        internal bool IsInstrumented
        {
            get
            {
                return _isInstrumented == 1 && ExceptionDebuggingProcessor != null && !string.IsNullOrEmpty(ProbeId);
            }
        }

        private bool ShouldInstrument()
        {
            if (Interlocked.CompareExchange(ref _isInstrumented, 1, 0) == 0)
            {
                ProbeId = Guid.NewGuid().ToString();
                ExceptionDebuggingProcessor = new ExceptionDebuggingProcessor(ProbeId, Method);
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

                    var processor = new ExceptionProbeProcessor(probe, @case.ExceptionId.ExceptionTypes, parentProbes: parentProbes, childProbes: childProbes);
                    @case.Processors.TryAdd(processor, 0);
                    ExceptionDebuggingProcessor?.AddProbeProcessor(processor);
                }
            }

            foreach (var probe in probes.Where(p => p.IsInstrumented))
            {
                probe.ExceptionDebuggingProcessor?.InvalidateEnterLeave();
            }
        }

        internal void AddExceptionCase(ExceptionCase @case, bool isPartOfCase)
        {
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
                    if (!ProbeExpressionsProcessor.Instance.TryAddProbeProcessor(ProbeId, ExceptionDebuggingProcessor))
                    {
                        Log.Error("Could not add ExceptionDebuggingProcessor. Method: {TypeName}.{MethodName}", Method.Method.DeclaringType?.Name, Method.Method.Name);
                    }

                    InstrumentationRequester.Instrument(ProbeId!, Method.Method);
                }

                _exceptionCases.Add(@case);
                ProcessCase(@case);
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

        public bool Equals(ExceptionDebuggingProbe other)
        {
            return Method.Equals(other.Method);
        }

        public override bool Equals(object? obj)
        {
            return obj is ExceptionDebuggingProbe other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
    }
}
