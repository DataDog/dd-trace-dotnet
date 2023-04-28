// <copyright file="RumLibraryInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Rum;

internal class RumLibraryInvoker
{
#if NETFRAMEWORK
    private const string DllName = "ddwaf.dll";
#else
    private const string DllName = "ddwaf";
#endif

    private readonly ScanDelegate _scanDelegate;

    private readonly IDatadogLogger _log = DatadogLogging.GetLoggerFor(typeof(RumLibraryInvoker));

    private RumLibraryInvoker(IntPtr libraryHandle)
    {
        ExportErrorHappened = false;
        _scanDelegate = GetDelegateForNativeFunction<ScanDelegate>(libraryHandle, "scan");
    }

    private delegate RumScanStatus ScanDelegate(ref RumScanResultStruct result, ref string document);

    internal bool ExportErrorHappened { get; private set; }

    internal static LibraryInitializationResult Initialize(string libVersion = null)
    {
        var fd = FrameworkDescription.Instance;

        var libName = "libdocumentScan.dylib";
        var runtimeIds = LibraryLocationHelper.GetRuntimeIds(fd);

        // libName or runtimeIds being null means platform is not supported
        // no point attempting to load the library
        IntPtr libraryHandle;
        if (libName != null && runtimeIds != null)
        {
            var paths = new List<string>() { "/Users/flavien.darche/Documents/rum-document-scan/cmake-build-debug/" };
            if (!LibraryLocationHelper.TryLoadLibraryFromPaths(libName, paths, out libraryHandle))
            {
                return LibraryInitializationResult.FromLibraryLoadError();
            }
        }
        else
        {
            Log.Error("Lib name or runtime ids is null, current platform {fd} is likely not supported", fd.ToString());
            return LibraryInitializationResult.FromPlatformNotSupported();
        }

        var wafLibraryInvoker = new RumLibraryInvoker(libraryHandle);
        if (wafLibraryInvoker.ExportErrorHappened)
        {
            Log.Error("Waf library couldn't initialize properly because of missing methods in native library, please make sure the tracer has been correctly installed and that previous versions are correctly uninstalled.");
            return LibraryInitializationResult.FromExportErrorHappened();
        }

        return LibraryInitializationResult.FromSuccess(wafLibraryInvoker);
    }

    private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName, out IntPtr funcPtr)
        where T : Delegate
    {
        funcPtr = Datadog.Trace.AppSec.Waf.NativeBindings.NativeLibrary.GetExport(handle, functionName);
        if (funcPtr == IntPtr.Zero)
        {
            _log.Error("No function of name {FunctionName} exists on rum lib object", functionName);
            ExportErrorHappened = true;
            return null;
        }

        _log.Debug("GetDelegateForNativeFunction {FunctionName} -  {FuncPtr}: ", functionName, funcPtr);
        return (T)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
    }

    private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName)
        where T : Delegate => GetDelegateForNativeFunction<T>(handle, functionName, out _);

    internal RumScanStatus Scan(ref RumScanResultStruct result, ref string document) => _scanDelegate(ref result, ref document);
}
