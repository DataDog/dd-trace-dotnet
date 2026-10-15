// <copyright file="NativeObjectRoots.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Keeps the native-callable wrappers handed to the native tracer alive: their finalizers free the vtables, so a
/// collected wrapper would leave the native side with a dangling pointer.
/// </summary>
internal static class NativeObjectRoots
{
    private static readonly List<object> Roots = new();

    public static T Add<T>(T wrapper)
        where T : notnull
    {
        lock (Roots)
        {
            Roots.Add(wrapper);
        }

        return wrapper;
    }

    public static void Clear()
    {
        lock (Roots)
        {
            foreach (var root in Roots)
            {
                (root as IDisposable)?.Dispose();
            }

            Roots.Clear();
        }
    }
}
