// <copyright file="CoverageEventHandler.cs" company="Datadog">
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
/// Coverage event handler
/// </summary>
internal abstract class CoverageEventHandler
{
    private readonly AsyncLocal<CoverageContextContainer?> _asyncContext;
    private readonly List<Action> _coverageContextContainerChangeActions;

    protected CoverageEventHandler()
    {
        _coverageContextContainerChangeActions = new();
        _asyncContext = new(ValueChangedHandler);
    }

    /// <summary>
    /// Gets the coverage global container
    /// </summary>
    internal CoverageContextContainer? Container => _asyncContext.Value;

    /// <summary>
    /// Start session
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StartSession()
    {
        _asyncContext.Value = new CoverageContextContainer();
    }

    /// <summary>
    /// End async session
    /// </summary>
    /// <returns>Object instance with the final coverage report</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? EndSession()
    {
        if (_asyncContext.Value is { } context)
        {
            _asyncContext.Value = null;
            return OnSessionFinished(context.CloseContext());
        }

        return null;
    }

    internal void AddContextContainerChangeAction(Action action)
    {
        lock (_coverageContextContainerChangeActions)
        {
            _coverageContextContainerChangeActions.Add(action);
        }
    }

    /// <summary>
    /// Method called when a session is finished to process all coverage raw data.
    /// </summary>
    /// <param name="modules">Coverage raw data</param>
    /// <returns>Instance of the final coverage report</returns>
    protected abstract object? OnSessionFinished(ModuleValue[] modules);

    private void ValueChangedHandler(AsyncLocalValueChangedArgs<CoverageContextContainer?> obj)
    {
        lock (_coverageContextContainerChangeActions)
        {
            foreach (var action in _coverageContextContainerChangeActions)
            {
                action();
            }
        }
    }
}
