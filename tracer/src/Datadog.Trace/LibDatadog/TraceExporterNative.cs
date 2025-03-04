// <copyright file="TraceExporterNative.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

#pragma warning disable SA1300
internal class TraceExporterNative
{
    private const string DllName = "datadog_profiling_ffi";

    /// <summary>
    /// Initializes static members of the <see cref="TraceExporterNative"/> class.
    /// TODO: Remove this workaround once we have libdatadog integration in the profiler and tracer.
    /// </summary>
    static TraceExporterNative()
    {
        CopyLibrary();
    }

    /// <summary>
    /// Copy the native library to same directory as the executing assembly.
    /// </summary>
    private static void CopyLibrary()
    {
        var rid = GetRuntimeIdentifier();
        var libraryName = GetLibraryFileName(DllName);
        var libraryPath = Path.Combine("runtimes", rid, "native", libraryName);

        var executingAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var destinationDirectory = Path.GetDirectoryName(executingAssemblyPath);
        if (destinationDirectory == null)
        {
            throw new InvalidOperationException("The directory of the executing assembly could not be determined.");
        }

        var destinationPath = Path.Combine(destinationDirectory, libraryName);
        File.Copy(libraryPath, destinationPath, overwrite: true);
    }

    /// <summary>
    /// Gets the platform-specific file name for the native library.
    /// </summary>
    private static string GetLibraryFileName(string libraryName)
    {
#if NETCOREAPP || NET5_0_OR_GREATER
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
#else
        // Fallback for .NET Framework
        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                return $"{libraryName}.dll";
            case PlatformID.Unix:
                return $"{libraryName}.so"; // .NET Framework assumes Linux for Unix
            case PlatformID.MacOSX:
                return $"{libraryName}.dylib";
        }
#endif
        throw new PlatformNotSupportedException("Current OS platform is not supported");
    }

    /// <summary>
    /// Gets the runtime identifier (RID) for the current platform and architecture.
    /// </summary>
    private static string GetRuntimeIdentifier()
    {
#if NETCOREAPP || NET5_0_OR_GREATER
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
            bool isMusl = IsAlpine();
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => isMusl ? "linux-musl-x64" : "linux-x64",
                Architecture.Arm64 => isMusl ? "linux-musl-arm64" : "linux-arm64",
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
        else
        {
            throw new PlatformNotSupportedException("Current OS platform is not supported");
        }
#else
        // Fallback for .NET Framework
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return Environment.Is64BitOperatingSystem ? "win-x64" : "win-x86";
        }
        else
        {
            throw new PlatformNotSupportedException("Only Windows is supported in .NET Framework");
        }
#endif
    }

    /// <summary>
    /// Check if the current OS is Alpine Linux.
    /// </summary>
    public static bool IsAlpine()
    {
        try
        {
            if (File.Exists("/etc/os-release"))
            {
                var strArray = File.ReadAllLines("/etc/os-release");
                foreach (var str in strArray)
                {
                    if (str.StartsWith("ID=", StringComparison.Ordinal))
                    {
                        return str.Substring(3).Trim('"', '\'') == "alpine";
                    }
                }
            }
        }
        catch
        {
            // ignore error checking if the file doesn't exist or we can't read it
        }

        return false;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_new(out IntPtr outHandle, SafeHandle config);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ddog_trace_exporter_error_free(IntPtr error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ddog_trace_exporter_free(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_send(SafeHandle handle, ByteSlice trace, UIntPtr traceCount, ref IntPtr response);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ddog_trace_exporter_config_new(out IntPtr outHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ddog_trace_exporter_config_free(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_url(SafeHandle config, CharSlice url);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_tracer_version(SafeHandle config, CharSlice version);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_language(SafeHandle config, CharSlice lang);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_lang_version(SafeHandle config, CharSlice version);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_lang_interpreter(SafeHandle config, CharSlice interpreter);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_hostname(SafeHandle config, CharSlice hostname);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_env(SafeHandle config, CharSlice env);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_version(SafeHandle config, CharSlice version);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorHandle ddog_trace_exporter_config_set_service(SafeHandle config, CharSlice service);
}
#pragma warning restore SA1300
