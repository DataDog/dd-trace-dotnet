// <copyright file="MethodUniqueIdentifier.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal readonly record struct MethodUniqueIdentifier(Guid Mvid, int MethodToken, MethodBase Method)
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(Mvid, MethodToken);
        }
    }

    internal readonly struct ExceptionCase : IEquatable<ExceptionCase>
    {
        private readonly int _hashCode;

        public ExceptionCase(ExceptionIdentifier exceptionId, ExceptionDebuggingProbe[] probes)
        {
            ExceptionId = exceptionId;
            Probes = probes;
            _hashCode = ComputeHashCode();
        }

        public ExceptionIdentifier ExceptionId { get; }

        public ExceptionDebuggingProbe[] Probes { get; }

        public ConcurrentDictionary<ExceptionProbeProcessor, byte> Processors { get; } = new();

        private int ComputeHashCode()
        {
            var hashCode = new HashCode();

            foreach (var probe in Probes)
            {
                hashCode.Add(probe);
            }

            return hashCode.ToHashCode();
        }

        public bool Equals(ExceptionCase other)
        {
            return Probes.SequenceEqual(other.Probes);
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((ExceptionCase)obj);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override string ToString()
        {
            var probesInfo = Probes == null ? "null" : $"{Probes.Length} probes";
            var processorsCount = Processors?.Count ?? 0;
            return $"ExceptionCase: ExceptionId={ExceptionId}, Probes=[{probesInfo}], Processors={processorsCount}";
        }
    }

    internal readonly struct ExceptionIdentifier : IEquatable<ExceptionIdentifier>
    {
        private readonly int _hashCode;

        public ExceptionIdentifier(HashSet<Type> exceptionTypes, ParticipatingFrame[] stackTrace, ErrorOriginKind errorOrigin)
        {
            ExceptionTypes = exceptionTypes;
            StackTrace = stackTrace;
            ErrorOrigin = errorOrigin;
            _hashCode = ComputeHashCode();
        }

        public HashSet<Type> ExceptionTypes { get; }

        public ParticipatingFrame[] StackTrace { get; }

        public ErrorOriginKind ErrorOrigin { get; }

        private int ComputeHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(ErrorOrigin);

            foreach (var exceptionType in ExceptionTypes)
            {
                hashCode.Add(exceptionType);
            }

            foreach (var frame in StackTrace)
            {
                hashCode.Add(frame);
            }

            return hashCode.ToHashCode();
        }

        public bool Equals(ExceptionIdentifier other)
        {
            return ErrorOrigin == other.ErrorOrigin &&
                   ExceptionTypes.SequenceEqual(other.ExceptionTypes) &&
                   StackTrace.SequenceEqual(other.StackTrace);
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((ExceptionIdentifier)obj);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override string ToString()
        {
            var exceptionTypes = string.Join(", ", ExceptionTypes.Select(t => t.FullName));
            var stackTrace = string.Join("; ", StackTrace.Select(frame => frame.ToString()));
            return $"ErrorOrigin: {ErrorOrigin}, ExceptionTypes: [{exceptionTypes}], StackTrace: [{stackTrace}]";
        }
    }

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

        internal string ProbeId { get; private set; }

        internal MethodUniqueIdentifier Method { get; }

        internal ExceptionDebuggingProcessor ExceptionDebuggingProcessor { get; private set; }

        internal Status ProbeStatus { get; set; }

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

                return true;
            }

            return false;
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
                    ExceptionDebuggingProcessor.AddProbeProcessor(processor);
                }
            }

            foreach (var probe in probes.Where(p => p.IsInstrumented))
            {
                probe.ExceptionDebuggingProcessor.InvalidateEnterLeave();
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

                    // We don't care about sampling Exception Probes. To save memory, NopAdaptiveSampler is used.
                    ProbeRateLimiter.Instance.TryAddSampler(ProbeId, NopAdaptiveSampler.Instance);
                    if (!ProbeExpressionsProcessor.Instance.TryAddProbeProcessor(ProbeId, ExceptionDebuggingProcessor))
                    {
                        Log.Error("Could not add ExceptionDebuggingProcessor. Method: {TypeName}.{MethodName}", Method.Method.DeclaringType.Name, Method.Method.Name);
                        System.Diagnostics.Debugger.Break();
                    }

                    // TODO use full name
                    var rejitRequest = new NativeMethodProbeDefinition(ProbeId, Method.Method.DeclaringType.Name, Method.Method.Name, targetParameterTypesFullName: null);

                    DebuggerNativeMethods.InstrumentProbes(
                    new[] { rejitRequest },
                    Array.Empty<NativeLineProbeDefinition>(),
                    Array.Empty<NativeSpanProbeDefinition>(),
                    Array.Empty<NativeRemoveProbeRequest>());
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

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((ExceptionDebuggingProbe)obj);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
    }
}
