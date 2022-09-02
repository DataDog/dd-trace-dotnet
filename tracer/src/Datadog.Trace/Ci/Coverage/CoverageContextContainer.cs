// <copyright file="CoverageContextContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Coverage context container instance
/// </summary>
internal sealed class CoverageContextContainer
{
    private static readonly List<AsyncLocal<Tuple<ModuleValue, CoverageContextContainer>?>> AsyncLocals = new();
    private readonly List<ModuleValue> _container = new();

    /// <summary>
    /// Gets or sets a value indicating whether if the coverage is enabled for the context
    /// </summary>
    public bool Enabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal set;
    }

        = true;

    /// <summary>
    /// Adds an AsyncLocal to be cleared on container close.
    /// </summary>
    /// <param name="asyncLocal">Async local of the module</param>
    public static void AddAsyncLocal(AsyncLocal<Tuple<ModuleValue, CoverageContextContainer>?> asyncLocal)
    {
        lock (AsyncLocals)
        {
            AsyncLocals.Add(asyncLocal);
        }
    }

    /// <summary>
    /// Stores module data into the context
    /// </summary>
    /// <param name="module">Module instance</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryAdd(ModuleValue module)
    {
        var container = _container;
        lock (container)
        {
            if (Enabled)
            {
                container.Add(module);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets modules data from the context
    /// </summary>
    /// <returns>Instruction array from the context</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ModuleValue[] CloseContext()
    {
        var container = _container;
        lock (container)
        {
            Enabled = false;
            var data = container.ToArray();
            container.Clear();
            lock (AsyncLocals)
            {
                foreach (var asyncLocal in AsyncLocals)
                {
                    asyncLocal.Value = null;
                }
            }

            return data;
        }
    }
}
