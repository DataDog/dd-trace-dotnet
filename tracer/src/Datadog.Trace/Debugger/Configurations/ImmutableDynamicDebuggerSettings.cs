// <copyright file="ImmutableDynamicDebuggerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>


using System;

#nullable enable

namespace Datadog.Trace.Debugger.Configurations
{
    internal class ImmutableDynamicDebuggerSettings : IEquatable<ImmutableDynamicDebuggerSettings>
    {
        public bool? DynamicInstrumentationEnabled { get; init; }

        public bool? ExceptionReplayEnabled { get; init; }

        public bool? SpanOriginEntryEnabled { get; init; }

        public bool? SpanOriginExitEnabled { get; init; }

        public bool? TriggerProbeEnabled { get; init; }

        public bool Equals(ImmutableDynamicDebuggerSettings? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return
                DynamicInstrumentationEnabled == DynamicInstrumentationEnabled
             && ExceptionReplayEnabled == ExceptionReplayEnabled
             && SpanOriginEntryEnabled == SpanOriginEntryEnabled
             && SpanOriginExitEnabled == SpanOriginExitEnabled
             && TriggerProbeEnabled == TriggerProbeEnabled;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
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

            return this.Equals((ImmutableDynamicDebuggerSettings)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                DynamicInstrumentationEnabled,
                ExceptionReplayEnabled,
                SpanOriginEntryEnabled,
                SpanOriginExitEnabled,
                TriggerProbeEnabled);
        }
    }
}
