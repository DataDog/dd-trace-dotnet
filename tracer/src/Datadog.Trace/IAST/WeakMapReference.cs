// <copyright file = "WeakMapReference.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike;

namespace Datadog.Trace.Iast;

internal class WeakMapReference : IWeakAware
{
    internal WeakMapReference(object key)
    {
        Weak = new WeakReference(key);
        Hash = key.GetHashCode();
    }

    public WeakReference Weak { get; }

    public bool IsAlive => Weak.IsAlive;

    public int? Hash { get; }

    public override int GetHashCode()
    {
        return Hash ?? 0;
    }

    public override bool Equals(object? obj)
    {
        if (obj is WeakMapReference weak)
        {
            return Weak?.Target?.Equals(weak?.Weak?.Target) ?? false;
        }
        else
        {
            return Weak?.Target?.Equals(obj) ?? false;
        }
    }
}
