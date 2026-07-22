// <copyright file="CoverageReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private static CoverageEventHandler _handler = CreateDefaultHandler(TestOptimization.Instance.Settings);

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
