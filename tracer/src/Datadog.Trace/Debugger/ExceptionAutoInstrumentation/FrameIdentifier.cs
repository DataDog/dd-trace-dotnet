// <copyright file="FrameIdentifier.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal readonly record struct FrameIdentifier
    {
        public FrameIdentifier(string typeName, string methodName)
        {
            this.TypeName = typeName;
            this.MethodName = methodName;
        }

        public string TypeName { get; }

        public string MethodName { get; }
    }

    internal readonly record struct Probe
    {
        public Probe(FrameIdentifier frame, string probeId)
        {
            this.Frame = frame;
            this.ProbeId = probeId;
        }

        public FrameIdentifier Frame { get; }

        public string ProbeId { get; }
    }

    internal record struct ExceptionCase(Probe[] Probes, int InvocationCount = 0);

    internal class ExceptionIdentifier : IEquatable<ExceptionIdentifier>
    {
        public ExceptionIdentifier(string exceptionType, FrameIdentifier[] stackTrace)
        {
            ExceptionType = exceptionType;
            StackTrace = stackTrace;
        }

        public string ExceptionType { get; }

        public FrameIdentifier[] StackTrace { get; }

        public bool Equals(ExceptionIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ExceptionType == other.ExceptionType &&
                   StackTrace.SequenceEqual(other.StackTrace);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((ExceptionIdentifier)obj);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(ExceptionType);
            foreach (var frame in StackTrace)
            {
                hashCode.Add(frame);
            }

            return hashCode.ToHashCode();
        }
    }
}
