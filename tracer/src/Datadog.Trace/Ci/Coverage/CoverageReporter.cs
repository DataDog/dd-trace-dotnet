// <copyright file="CoverageReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Coverage Reporter
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class CoverageReporter
{
    private static CoverageEventHandler? _handler;

    /// <summary>
    /// Gets or sets coverage handler
    /// </summary>
    /// <exception cref="ArgumentNullException">If value is null</exception>
    internal static CoverageEventHandler Handler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LazyInitializer.EnsureInitialized(ref _handler, static () => CreateDefaultHandler(TestOptimization.Instance.Settings))!;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Volatile.Write(ref _handler, value ?? throw new ArgumentNullException(nameof(value)));
    }

    internal static CoverageContextContainer? Container => Handler.Container;

    internal static CoverageContextContainer GlobalContainer => Handler.GlobalContainer;

    /// <summary>
    /// Publishes the final global coverage snapshot and seals the process output, if coverage was used by this process.
    /// </summary>
    /// <returns>True when global coverage is not active or the process output was sealed completely.</returns>
    internal static bool FinalizeGlobalCoverage()
    {
        var handler = Volatile.Read(ref _handler);
        return handler is not DefaultWithGlobalCoverageEventHandler globalHandler || globalHandler.FinalizeAndSeal();
    }

    /// <summary>
    /// Creates the default coverage event handler for the current CI Visibility coverage mode.
    /// </summary>
    internal static CoverageEventHandler CreateDefaultHandler(TestOptimizationSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        return settings.TestsSkippingEnabled == true && StringUtil.IsNullOrWhiteSpace(settings.CodeCoveragePath)
                   ? new DefaultCoverageEventHandler()
                   : new DefaultWithGlobalCoverageEventHandler(configuredOutputDirectory: settings.CodeCoveragePath);
    }
}
