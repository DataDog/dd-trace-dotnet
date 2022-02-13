// <copyright file="Sampling.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Configurations.Models;

internal struct Sampling : IEquatable<Sampling>
{
    public double SnapshotsPerSecond { get; set; }

    public bool Equals(Sampling other)
    {
        return SnapshotsPerSecond.Equals(other.SnapshotsPerSecond);
    }

    public override bool Equals(object obj)
    {
        return obj is Sampling other && Equals(other);
    }

    public override int GetHashCode()
    {
        return SnapshotsPerSecond.GetHashCode();
    }
}
