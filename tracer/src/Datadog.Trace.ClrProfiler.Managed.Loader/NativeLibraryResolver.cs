// <copyright file="NativeLibraryResolver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// Provides functionality to resolve and load native libraries from specified directories.
    /// This resolver is compatible with .NET Core 2.0 and above, handling library loading across Windows, Linux, and macOS platforms.
    /// </summary>
    public static class NativeLibraryResolver
    {
        /// <summary>
        /// Windows API function to load a native library.
        /// </summary>
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        /// <summary>
        /// POSIX API function to load a native library.
        /// </summary>
        [DllImport("libdl")]
#pragma warning disable SA1300 // Element should begin with upper-case letter
        private static extern IntPtr dlopen(string fileName, int flags);
#pragma warning restore SA1300 // Element should begin with upper-case letter

        /// <summary>
        /// POSIX API function to get the most recent error that occurred during library loading.
        /// </summary>
        [DllImport("libdl")]
#pragma warning disable SA1300 // Element should begin with upper-case letter
        private static extern IntPtr dlerror();
#pragma warning restore SA1300 // Element should begin with upper-case letter

        /// <summary>
        /// Flag for dlopen indicating that all symbols should be resolved immediately.
        /// </summary>
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1310 // Field names should not contain underscore
        private const int RTLD_NOW = 2;
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1201 // Elements should appear in the correct order

        /// <summary>
        /// Loads a native library from the specified base path and library name.
        /// </summary>
        /// <param name="basePath">The base directory path where the 'runtimes' folder is located.</param>
        /// <param name="libraryName">The name of the library without platform-specific prefixes or extensions.</param>
        /// <param name="customRuntimesPath">Optional custom path relative to basePath where runtime-specific libraries are located. Defaults to "runtimes".</param>
        /// <returns>True if the library was loaded successfully; otherwise, false.</returns>
        /// <exception cref="DllNotFoundException">Thrown when the native library cannot be found or loaded.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown when the current platform or architecture is not supported.</exception>
        public static bool LoadNativeLibrary(string basePath, string libraryName, string customRuntimesPath = "runtimes")
        {
            string libraryPath = ResolveDllPath(basePath, libraryName, customRuntimesPath);
            if (!File.Exists(libraryPath))
            {
                throw new DllNotFoundException($"Native library not found at path: {libraryPath}");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return LoadLibrary(libraryPath) != IntPtr.Zero;
            }
            else
            {
                // Clear any existing errors
                dlerror();

                // Try to load the library
                IntPtr handle = dlopen(libraryPath, RTLD_NOW);

                // Check for errors
                IntPtr errorPtr = dlerror();
                if (handle == IntPtr.Zero || errorPtr != IntPtr.Zero)
                {
                    throw new DllNotFoundException($"Failed to load native library from path: {libraryPath}");
                }

                return true;
            }
        }

        /// <summary>
        /// Resolves the full path to the native library based on the current runtime identifier.
        /// </summary>
        private static string ResolveDllPath(string basePath, string libraryName, string customRuntimesPath)
        {
            string rid = GetRuntimeIdentifier();
            string libraryFileName = GetLibraryFileName(libraryName);

            return Path.Combine(basePath, customRuntimesPath, rid, "native", libraryFileName);
        }

        /// <summary>
        /// Gets the runtime identifier (RID) for the current platform and architecture.
        /// </summary>
        private static string GetRuntimeIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "win-x64",
                    Architecture.X86 => "win-x86",
                    Architecture.Arm64 => "win-arm64",
                    _ => throw new PlatformNotSupportedException($"Architecture {RuntimeInformation.ProcessArchitecture} is not supported")
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "linux-x64",
                    Architecture.Arm64 => "linux-arm64",
                    _ => throw new PlatformNotSupportedException($"Architecture {RuntimeInformation.ProcessArchitecture} is not supported")
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "osx-x64",
                    Architecture.Arm64 => "osx-arm64",
                    _ => throw new PlatformNotSupportedException($"Architecture {RuntimeInformation.ProcessArchitecture} is not supported")
                };
            }

            throw new PlatformNotSupportedException("Current OS platform is not supported");
        }

        /// <summary>
        /// Gets the platform-specific file name for the native library.
        /// </summary>
        private static string GetLibraryFileName(string libraryName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"{libraryName}.dll";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return $"{libraryName}.so";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"{libraryName}.dylib";
            }

            throw new PlatformNotSupportedException("Current OS platform is not supported");
        }
    }
}
#endif
