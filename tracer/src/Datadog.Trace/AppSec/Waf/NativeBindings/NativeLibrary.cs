// <copyright file="NativeLibrary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

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

        [Flags]
        public enum FORMAT_MESSAGE : int
        {
            NONE = 0,
            ALLOCATE_BUFFER = 0x00000100,
            ARGUMENT_ARRAY = 0x00002000,
            FROM_HMODULE = 0x00000800,
            FROM_STRING = 0x00000400,
            FROM_SYSTEM = 0x00001000,
            IGNORE_INSERTS = 0x00000200,
            MAX_WIDTH_MASK = 0x000000FF
        }

        internal static bool TryLoad(string libraryPath, out IntPtr handle)
        {
            if (libraryPath == null)
            {
                throw new ArgumentNullException(nameof(libraryPath));
            }

            handle = IntPtr.Zero;

            try
            {
                if (isPosixLike)
                {
                    handle = LoadPosixLibrary(libraryPath);
                }
                else
                {
                    handle = LoadWindowsLibrary(libraryPath);
                }
            }
            catch (Exception ex)
            {
                // as this method is prefixed "Try" we shouldn't throw, but experience has
                // shown that unforseen circumstance can lead to exceptions being thrown
                Log.Error("Error occured while trying to load library from {LibraryPath}", libraryPath, ex);
            }

            return handle != IntPtr.Zero;
        }

        private static IntPtr LoadWindowsLibrary(string libraryPath)
        {
            var handle = LoadLibrary(libraryPath);
            if (handle == IntPtr.Zero)
            {
                LogWindowsError("LoadLibrary");
            }

            return handle;
        }

        private static void LogWindowsError(string source)
        {
            var hresult = GetLastError();
            if (hresult != 0)
            {
                var msgOut = StringBuilderCache.Acquire(256);
                var size = FormatMessage(FORMAT_MESSAGE.ALLOCATE_BUFFER | FORMAT_MESSAGE.FROM_SYSTEM | FORMAT_MESSAGE.IGNORE_INSERTS, IntPtr.Zero, hresult, 0, ref msgOut, (uint)msgOut.Capacity, IntPtr.Zero);
                var message = StringBuilderCache.GetStringAndRelease(msgOut).Trim();
                Log.Warning("Error occurred when calling {Function} message was: 0x{HResult}: {Message}", source, hresult.ToString("X8"), message);
            }
            else
            {
                Log.Warning("Error occurred when calling {Function} but no error message was set", source);
            }
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
                var exportPtr = GetProcAddress(handle, name);
                if (exportPtr == IntPtr.Zero)
                {
                    LogWindowsError("GetExport");
                }

                return exportPtr;
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
                // warning, since in some cases dddlerror returns a message when an error didn't occur or was recoverable
                Log.Warning("'{Op}' dddlerror returned: {Error}", op, error);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("Kernel32.dll")]
        private static extern uint GetLastError();

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern uint FormatMessage(
            FORMAT_MESSAGE dwFlags,
            IntPtr lpSource,
            uint dwMessageId,
            uint dwLanguageId,
            ref StringBuilder lpBuffer,
            uint nSize,
            IntPtr pArguments);

        private static class NonWindows
        {
            // These are re-written by the native tracer to point to the correct location,
            // but if running in a managed-only context (i.e. unit tests) they need to have the right file name
#pragma warning disable SA1300 // Element should begin with upper-case letter
            [DllImport("Datadog.Tracer.Native")]
            internal static extern IntPtr dddlopen(string fileName, int flags);

            [DllImport("Datadog.Tracer.Native")]
            internal static extern IntPtr dddlerror();

            [DllImport("Datadog.Tracer.Native")]
            internal static extern IntPtr dddlsym(IntPtr hModule, string lpProcName);
        }
    }
}
