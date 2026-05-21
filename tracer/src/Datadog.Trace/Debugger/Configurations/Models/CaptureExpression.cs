// <copyright file="CaptureExpression.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Debugger.Configurations.Models
{
    internal sealed class CaptureExpression : IEquatable<CaptureExpression>
    {
        public string? Name { get; set; }

        public SnapshotSegment? Expr { get; set; }

        public Capture? Capture { get; set; }

        public bool Equals(CaptureExpression? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Name == other.Name && Equals(Expr, other.Expr) && Equals(Capture, other.Capture);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((CaptureExpression)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Expr, Capture);
        }
    }
}
