// <copyright file="NativeLibrary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    /// <summary>
    /// APIs for managing Native Libraries
    /// </summary>
    internal partial class NativeLibrary
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<NativeLibrary>();

        private static bool isPosixLike =
#if NETFRAMEWORK
            false;
#else
            !RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif

        internal static bool TryLoad(string libraryPath, out IntPtr handle)
        {
            if (libraryPath == null)
            {
                throw new ArgumentNullException(nameof(libraryPath));
            }

            if (isPosixLike)
            {
                handle = LoadPosixLibrary(libraryPath);
            }
            else
            {
                handle = LoadLibrary(libraryPath);
            }

            return handle != IntPtr.Zero;
        }

        internal static IntPtr GetExport(IntPtr handle, string name)
        {
            if (isPosixLike)
            {
                var exportPtr = NonWindows.dddlsym(handle, name);
                ReadDlerror("dddlsym");
                return exportPtr;
            }
            else
            {
                return GetProcAddress(handle, name);
            }
        }

        private static IntPtr LoadPosixLibrary(string path)
        {
            const int RTLD_NOW = 2;
            var addr = NonWindows.dddlopen(path, RTLD_NOW);
            ReadDlerror("dddlopen");

            return addr;
        }

        private static void ReadDlerror(string op)
        {
            var errorPtr = NonWindows.dddlerror();
            if (errorPtr != IntPtr.Zero)
            {
                var error = Marshal.PtrToStringAnsi(errorPtr);
                Log.Error($"Error during '{op}': {error}");
            }
        }

#pragma warning disable SA1300 // Element should begin with upper-case letter
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        private static class NonWindows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            internal static extern IntPtr dddlopen(string fileName, int flags);

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            internal static extern IntPtr dddlerror();

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            internal static extern IntPtr dddlsym(IntPtr hModule, string lpProcName);
        }
    }
}
