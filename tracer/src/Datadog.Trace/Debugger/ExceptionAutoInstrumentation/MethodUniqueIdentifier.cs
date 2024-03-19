// <copyright file="MethodUniqueIdentifier.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#nullable enable
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

        public override bool Equals(object? obj)
        {
            return obj is ExceptionCase other && Equals(other);
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

        public override bool Equals(object? obj)
        {
            return obj is ExceptionIdentifier && Equals((ExceptionIdentifier)obj);
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
}
