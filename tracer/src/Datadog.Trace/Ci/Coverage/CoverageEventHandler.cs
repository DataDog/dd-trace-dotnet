// <copyright file="CoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Coverage event handler
/// </summary>
internal abstract class CoverageEventHandler
{
    private readonly AsyncLocal<CoverageContextContainer?> _asyncContext = new();

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
    /// Gets if there is an active session for the current context
    /// </summary>
    /// <returns>True if a session is active; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSessionActiveForCurrentContext()
    {
        return _asyncContext.Value?.Enabled ?? false;
    }

    /// <summary>
    /// Enable coverage for current context (An active coverage session is required)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnableCoverageForCurrentContext()
    {
        if (_asyncContext.Value is { } contextContainer)
        {
            contextContainer.Enabled = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Disable coverage for current context (An active coverage session is required)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDisableCoverageForCurrentContext()
    {
        if (_asyncContext.Value is { } contextContainer)
        {
            contextContainer.Enabled = false;
            return true;
        }

        return false;
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

    /// <summary>
    /// Method called when a session is finished to process all coverage raw data.
    /// </summary>
    /// <param name="modules">Coverage raw data</param>
    /// <returns>Instance of the final coverage report</returns>
    protected abstract object? OnSessionFinished(ModuleValue[] modules);
}
