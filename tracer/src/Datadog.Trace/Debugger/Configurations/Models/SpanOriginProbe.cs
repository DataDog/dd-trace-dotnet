// <copyright file="SpanOriginProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

#nullable enable

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal enum SpanOriginKind
    {
        Entry,
        Exit
    }

    internal class SpanOriginProbe : ProbeDefinition, IEquatable<SpanOriginProbe>
    {
        private SpanOriginKind Kind { get; set; }

        public bool Equals(SpanOriginProbe other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && Kind == other.Kind;
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

            return Equals((SpanOriginProbe)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), (int)Kind);
        }
    }
}
