// <copyright file="Allocation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike;

namespace Datadog.Trace.FeatureFlags.Exposure;

internal class Allocation(string key)
{
    public string Key { get; } = key;

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }

        if (obj is Allocation that)
        {
            return Key.Equals(that.Key);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }
}
