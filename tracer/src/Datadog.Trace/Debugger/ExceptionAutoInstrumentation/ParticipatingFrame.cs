// <copyright file="ParticipatingFrame.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Reflection;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal enum ParticipatingFrameState
    {
        Default,
        Blacklist
    }

    internal readonly struct ParticipatingFrame
    {
        private const int UndefinedIlOffset = -1;

        private ParticipatingFrame(MethodBase method, ParticipatingFrameState state, int ilOffset = UndefinedIlOffset)
        {
            Method = method;
            MethodIdentifier = new MethodUniqueIdentifier(method.Module.ModuleVersionId, method.MetadataToken, method);
            ILOffset = ilOffset;
            State = state;
        }

        public ParticipatingFrame(StackFrame stackFrame, ParticipatingFrameState state)
            : this(stackFrame.GetMethod() ?? throw new ArgumentNullException(nameof(stackFrame.GetMethod)), state, stackFrame.GetILOffset())
        {
        }

        public ParticipatingFrameState State { get; }

        public MethodBase Method { get; }

        public MethodUniqueIdentifier MethodIdentifier { get; }

        public int ILOffset { get; }

        public override int GetHashCode()
        {
            return MethodIdentifier.GetHashCode();
        }

        public bool Equals(ParticipatingFrame other)
        {
            return MethodIdentifier.Equals(other.MethodIdentifier);
        }

        public override bool Equals(object? obj)
        {
            return obj is ParticipatingFrame other && Equals(other);
        }

        public override string ToString()
        {
            var methodName = Method?.DeclaringType?.FullName + "." + Method?.Name ?? "Unknown Method";
            var blacklistStatus = State == ParticipatingFrameState.Blacklist ? "Blacklisted" : "Not Blacklisted";
            var ilOffsetInfo = ILOffset != UndefinedIlOffset ? $"IL Offset: {ILOffset}" : "IL Offset: Undefined";
            return $"ParticipatingFrame: Method={methodName}, State={State}, {ilOffsetInfo}, {blacklistStatus}";
        }
    }
}
