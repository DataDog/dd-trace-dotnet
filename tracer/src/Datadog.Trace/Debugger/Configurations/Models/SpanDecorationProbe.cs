// <copyright file="SpanDecorationProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal enum TargetSpan
    {
        Active,
        Root
    }

    internal class SpanDecorationProbe : ProbeDefinition, IEquatable<SpanDecorationProbe>
    {
        public TargetSpan TargetSpan { get; set; }

        public Decoration[] Decorations { get; set; }

        public bool Equals(SpanDecorationProbe other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && TargetSpan == other.TargetSpan && Decorations.NullableSequentialEquals(other.Decorations);
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

            return Equals((SpanDecorationProbe)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), (int)TargetSpan, Decorations);
        }
    }
}
