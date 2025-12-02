// <copyright file="LibraryInitializationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf.Initialization;

internal sealed class LibraryInitializationResult
{
    public LibraryInitializationResult(LoadStatus status)
    {
        Status = status;
        System.Diagnostics.Debug.Assert(Status != LoadStatus.Ok, "Library initialization should not be successful here");
    }

    public LibraryInitializationResult(WafLibraryInvoker? wafLibraryInvoker)
    {
        Status = LoadStatus.Ok;
        WafLibraryInvoker = wafLibraryInvoker;
    }

    [Flags]
    public enum LoadStatus
    {
        Ok = 0,
        LibraryLoad = 1 << 0,
        ExportError = 1 << 1,
        PlatformNotSupported = 1 << 2,
        VersionNotCompatible = 1 << 3,
    }

    internal WafLibraryInvoker? WafLibraryInvoker { get; }

    internal LoadStatus Status { get; }

    internal bool Success => Status == LoadStatus.Ok;
}
