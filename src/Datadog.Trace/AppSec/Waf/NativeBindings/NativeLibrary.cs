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
                return dlsym(handle, name);
            }
            else
            {
                return GetProcAddress(handle, name);
            }
        }

        private static IntPtr LoadPosixLibrary(string path)
        {
            const int RTLD_NOW = 2;
            var addr = dlopen(path, RTLD_NOW);
            if (addr == IntPtr.Zero)
            {
                // Not using NanosmgException because it depends on nn_errno.
                var error = Marshal.PtrToStringAnsi(dlerror());
                Log.Error("Error loading library: " + error);
            }

            return addr;
        }

#pragma warning disable SA1300 // Element should begin with upper-case letter

        [DllImport("dl")]
        private static extern IntPtr dlopen(string fileName, int flags);

        [DllImport("dl")]
        private static extern IntPtr dlerror();

        [DllImport("dl")]
        private static extern IntPtr dlsym(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    }
}
