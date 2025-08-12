// <copyright file="LibDatadogAvailaibilityHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Util;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// *This class should NOT contain any direct Logger field, nor methods should log*.
/// LibDatadogAvailable factory is used when building settings for the tracer, if a logger is instantiated in this path, it creates an infinite loop as the logger itself will try to build settings
/// The Lazy will loop on itself and end up in a InvalidOperationException as Value ends up calling itself
/// </summary>
internal static class LibDatadogAvailaibilityHelper
{
    // This will never change, so we use a lazy to cache the result.
    private static readonly Lazy<LibDatadogAvailableResult> LibDatadogAvailable = new(() => new(NativeMethods.IsLibdatadogAvailable()));

    public static LibDatadogAvailableResult IsLibDatadogAvailable => LibDatadogAvailable.Value;
}
