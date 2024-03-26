// <copyright file="LibraryInitializationResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf.Initialization;

internal class LibraryInitializationResult
{
    private LibraryInitializationResult(bool exportErrorHappened, bool libraryLoadError, bool platformNotSupported, bool versionNotCompatible, WafLibraryInvoker? wafLibraryInvoker = null)
    {
        ExportErrorHappened = exportErrorHappened;
        LibraryLoadError = libraryLoadError;
        PlatformNotSupported = platformNotSupported;
        WafLibraryInvoker = wafLibraryInvoker;
        VersionNotCompatible = versionNotCompatible;
    }

    internal WafLibraryInvoker? WafLibraryInvoker { get; }

    internal bool ExportErrorHappened { get; }

    internal bool LibraryLoadError { get; }

    internal bool PlatformNotSupported { get; }

    internal bool VersionNotCompatible { get; }

    internal bool Success => ExportErrorHappened == false && LibraryLoadError == false && PlatformNotSupported == false && VersionNotCompatible == false;

    internal static LibraryInitializationResult FromLibraryLoadError() => new(false, true, false, false);

    internal static LibraryInitializationResult FromExportErrorHappened() => new(true, false, false, false);

    internal static LibraryInitializationResult FromPlatformNotSupported() => new(false, false, true, false);

    internal static LibraryInitializationResult FromVersionNotCompatible() => new(false, false, false, true);

    public static LibraryInitializationResult FromSuccess(WafLibraryInvoker wafLibraryInvoker) => new(false, false, false, false, wafLibraryInvoker);
}
