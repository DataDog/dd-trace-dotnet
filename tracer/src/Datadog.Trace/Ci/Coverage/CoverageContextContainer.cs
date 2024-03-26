// <copyright file="CoverageContextContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Coverage context container instance
/// </summary>
internal sealed class CoverageContextContainer
{
    private readonly List<ModuleValue> _container = new();
    private ModuleValue? _currentModuleValue = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageContextContainer"/> class.
    /// </summary>
    /// <param name="state">State instance</param>
    public CoverageContextContainer(object? state = null)
    {
        State = state;
    }

    /// <summary>
    /// Gets or sets the context container state
    /// </summary>
    public object? State { get; set; }

    /// <summary>
    /// Gets the current module value
    /// </summary>
    /// <param name="module">Module instance</param>
    /// <returns>Current module instance</returns>
    internal ModuleValue? GetModuleValue(Module module)
    {
        if (_currentModuleValue is { } moduleValue && moduleValue.Module == module)
        {
            return moduleValue;
        }

        return GetModuleValueSlow(module);
    }

    private ModuleValue? GetModuleValueSlow(Module module)
    {
        var container = _container;
        lock (container)
        {
            for (var i = 0; i < container.Count; i++)
            {
                if (container[i] is { } moduleValueItem && moduleValueItem.Module == module)
                {
                    _currentModuleValue = moduleValueItem;
                    return moduleValueItem;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Stores module data into the context
    /// </summary>
    /// <param name="module">Module instance</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(ModuleValue module)
    {
        var container = _container;
        lock (container)
        {
            container.Add(module);
            _currentModuleValue = module;
        }
    }

    /// <summary>
    /// Clear context data
    /// </summary>
    internal void Clear()
    {
        var container = _container;
        lock (container)
        {
            foreach (var moduleValue in container)
            {
                moduleValue.Dispose();
            }

            container.Clear();
            _currentModuleValue = null;
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
            _currentModuleValue = null;
            return container.Count == 0 ? [] : container.ToArray();
        }
    }
}
