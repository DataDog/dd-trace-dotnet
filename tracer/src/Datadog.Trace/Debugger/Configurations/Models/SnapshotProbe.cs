// <copyright file="SnapshotProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Configurations.Models;

internal class SnapshotProbe : ProbeDefinition, IEquatable<SnapshotProbe>
{
    public Capture Capture { get; set; }

    public Sampling Sampling { get; set; }

    public bool Equals(SnapshotProbe other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return base.Equals(other) && Capture.Equals(other.Capture) && Sampling.Equals(other.Sampling);
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

        return Equals((SnapshotProbe)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Capture, Sampling);
    }
}
