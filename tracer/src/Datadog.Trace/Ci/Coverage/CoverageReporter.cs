// <copyright file="CoverageReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Coverage Reporter
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class CoverageReporter
{
    private static readonly List<Action<CoverageContextContainer?>> CoverageContextContainerChangeActions = new();
    private static CoverageEventHandler _handler = new DefaultWithGlobalCoverageEventHandler();

    /// <summary>
    /// Gets or sets coverage handler
    /// </summary>
    /// <exception cref="ArgumentNullException">If value is null</exception>
    internal static CoverageEventHandler Handler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _handler;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _handler = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal static CoverageContextContainer? Container => _handler.Container;

    internal static CoverageContextContainer GlobalContainer => _handler.GlobalContainer;

    internal static void AddContextContainerChangeAction(Action<CoverageContextContainer?> action)
    {
        lock (CoverageContextContainerChangeActions)
        {
            CoverageContextContainerChangeActions.Add(action);
        }
    }

    internal static void FireContextContainerChangeAction(CoverageContextContainer? ctx)
    {
        lock (CoverageContextContainerChangeActions)
        {
            foreach (var action in CoverageContextContainerChangeActions)
            {
                action?.Invoke(ctx);
            }
        }
    }
}
