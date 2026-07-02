// <copyright file="PosixDirectoryAccess.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Provides POSIX directory creation and permission validation for runner tracer home caches.
/// </summary>
internal static class PosixDirectoryAccess
{
    private const uint PosixDirectoryFileType = 0x4000;
    private const uint PosixSymbolicLinkFileType = 0xA000;
    private const uint PosixFileTypeMask = 0xF000;
    private const uint PosixGroupOrOtherWrite = 0x12; // 022
    private const uint PosixStickyBit = 0x200; // 01000
    private const uint PrivateDirectoryMode = 448; // 0700
    private const uint RootUserId = 0;

#if NETCOREAPP3_0_OR_GREATER
    private static readonly object NativeFileMetadataLock = new();
    private static string _nativeFileMetadataLibraryPath;
    private static bool _nativeFileMetadataLoadAttempted;
    private static IntPtr _nativeFileMetadataLibraryHandle;
    private static GetFileMetadataForPathDelegate _getFileMetadataForPath;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetFileMetadataForPathDelegate(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        int followSymlinks,
        out uint mode,
        out uint userId);
#endif

    /// <summary>
    /// Configures the native metadata helper used to inspect POSIX paths without shelling out to <c>stat(1)</c>.
    /// </summary>
    /// <param name="tracerHome">The source tracer home path containing the native tracer library.</param>
    internal static void ConfigureNativeFileMetadata(string tracerHome)
    {
#if NETCOREAPP3_0_OR_GREATER
        var nativeLibraryPath = GetNativeFileMetadataLibraryPath(tracerHome);
        lock (NativeFileMetadataLock)
        {
            if (string.Equals(_nativeFileMetadataLibraryPath, nativeLibraryPath, StringComparison.Ordinal))
            {
                return;
            }

            _nativeFileMetadataLibraryPath = nativeLibraryPath;
            _nativeFileMetadataLoadAttempted = false;
            _nativeFileMetadataLibraryHandle = IntPtr.Zero;
            _getFileMetadataForPath = null;
        }
#endif
    }

    /// <summary>
    /// Attempts to create a directory with private POSIX permissions.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    internal static void CreatePrivateDirectory(string path)
    {
        var result = Mkdir(path, PrivateDirectoryMode);
        if (result != 0 && !Directory.Exists(path))
        {
            throw new IOException($"Unable to create directory '{path}'. errno: {Marshal.GetLastWin32Error()}");
        }
    }

    /// <summary>
    /// Validates POSIX ownership and write permissions for an existing directory.
    /// </summary>
    /// <param name="path">The directory path to validate.</param>
    /// <param name="requireCurrentUserOwner">Whether the directory must be owned by the current effective user.</param>
    /// <param name="allowGroupOrOtherWrite">Whether group or other write bits are allowed.</param>
    /// <param name="allowStickyGroupOrOtherWrite">Whether sticky group or other writable directories are allowed.</param>
    /// <param name="allowTrustedSymlink">Whether symlinks owned by root or the current user are allowed.</param>
    internal static void ValidateDirectoryAccess(
        string path,
        bool requireCurrentUserOwner,
        bool allowGroupOrOtherWrite,
        bool allowStickyGroupOrOtherWrite = false,
        bool allowTrustedSymlink = false)
    {
        var directoryInfo = GetDirectoryInfo(path);
        var currentUserId = GetEffectiveUserId();
        if ((directoryInfo.Mode & PosixFileTypeMask) == PosixSymbolicLinkFileType)
        {
            if (!allowTrustedSymlink)
            {
                throw new IOException($"Path '{path}' must be a directory.");
            }

            if (!IsRootOrCurrentUser(directoryInfo.UserId, currentUserId))
            {
                throw new IOException($"Directory '{path}' symbolic link must be owned by root or the current user.");
            }

            directoryInfo = GetDirectoryInfo(path, followSymlinks: true);
        }

        if ((directoryInfo.Mode & PosixFileTypeMask) != PosixDirectoryFileType)
        {
            throw new IOException($"Path '{path}' must be a directory.");
        }

        if (requireCurrentUserOwner && directoryInfo.UserId != currentUserId)
        {
            throw new IOException($"Directory '{path}' must be owned by the current user.");
        }

        if (!allowGroupOrOtherWrite &&
            (directoryInfo.Mode & PosixGroupOrOtherWrite) != 0 &&
            !IsAllowedStickyDirectory(directoryInfo, currentUserId, allowStickyGroupOrOtherWrite))
        {
            throw new IOException($"Directory '{path}' must not be writable by group or other users.");
        }
    }

    /// <summary>
    /// Checks whether a broadly writable sticky directory is trusted as a POSIX ancestor.
    /// </summary>
    /// <param name="directoryInfo">The directory metadata to inspect.</param>
    /// <param name="currentUserId">The current effective user id.</param>
    /// <param name="allowStickyGroupOrOtherWrite">Whether sticky group or other writable directories are allowed.</param>
    /// <returns><c>true</c> when the sticky directory is owned by root or the current user.</returns>
    private static bool IsAllowedStickyDirectory(
        PosixDirectoryInfo directoryInfo,
        uint currentUserId,
        bool allowStickyGroupOrOtherWrite)
    {
        return allowStickyGroupOrOtherWrite &&
               (directoryInfo.Mode & PosixStickyBit) != 0 &&
               IsRootOrCurrentUser(directoryInfo.UserId, currentUserId);
    }

    /// <summary>
    /// Checks whether a POSIX owner uid is root or the current effective user.
    /// </summary>
    /// <param name="userId">The owner uid to inspect.</param>
    /// <param name="currentUserId">The current effective user id.</param>
    /// <returns><c>true</c> when the owner is root or the current effective user.</returns>
    private static bool IsRootOrCurrentUser(uint userId, uint currentUserId)
    {
        return userId == RootUserId || userId == currentUserId;
    }

    /// <summary>
    /// Gets the POSIX mode and owner uid for a directory.
    /// </summary>
    /// <param name="path">The directory path to inspect.</param>
    /// <param name="followSymlinks">Whether to follow symbolic links.</param>
    /// <returns>The POSIX directory metadata.</returns>
    private static PosixDirectoryInfo GetDirectoryInfo(string path, bool followSymlinks = false)
    {
        if (TryGetNativeDirectoryInfo(path, followSymlinks, out var nativeDirectoryInfo))
        {
            return nativeDirectoryInfo;
        }

        // struct stat layout varies across libc, CPU architectures, and macOS inode eras. The stat(1)
        // output format is stable enough for the two fields we need: mode and owner uid.
        var isMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                      string.Equals(FrameworkDescription.Instance.OSPlatform, OSPlatformName.MacOS, StringComparison.Ordinal) ||
                      FrameworkDescription.Instance.OSDescription.StartsWith("Darwin", StringComparison.OrdinalIgnoreCase);
        var statPath = isMacOs ? "/usr/bin/stat" : File.Exists("/usr/bin/stat") ? "/usr/bin/stat" : "/bin/stat";
        var processStartInfo = new ProcessStartInfo(statPath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        if (followSymlinks)
        {
            processStartInfo.ArgumentList.Add("-L");
        }

        processStartInfo.ArgumentList.Add(isMacOs ? "-f" : "-c");
        processStartInfo.ArgumentList.Add(isMacOs ? "%p %u" : "%f %u");
        processStartInfo.ArgumentList.Add(path);

        using var process = Process.Start(processStartInfo);
        if (process is null)
        {
            throw new IOException($"Unable to inspect directory '{path}'.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new IOException($"Unable to inspect directory '{path}'. {error}");
        }

        var values = output.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 2 || !uint.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            throw new IOException($"Unable to inspect directory '{path}'. Unexpected stat output: '{output.Trim()}'.");
        }

        uint mode;
        try
        {
            mode = Convert.ToUInt32(values[0], isMacOs ? 8 : 16);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new IOException($"Unable to inspect directory '{path}'. Unexpected stat mode: '{values[0]}'.", ex);
        }

        return new PosixDirectoryInfo(mode, userId);
    }

    private static bool TryGetNativeDirectoryInfo(string path, bool followSymlinks, out PosixDirectoryInfo directoryInfo)
    {
        directoryInfo = default;
#if NETCOREAPP3_0_OR_GREATER
        var getFileMetadataForPath = GetNativeFileMetadataForPath();
        if (getFileMetadataForPath is null)
        {
            return false;
        }

        try
        {
            var result = getFileMetadataForPath(path, followSymlinks ? 1 : 0, out var mode, out var userId);
            if (result != 0)
            {
                return false;
            }

            directoryInfo = new PosixDirectoryInfo(mode, userId);
            return true;
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

#if NETCOREAPP3_0_OR_GREATER
    private static GetFileMetadataForPathDelegate GetNativeFileMetadataForPath()
    {
        lock (NativeFileMetadataLock)
        {
            if (_nativeFileMetadataLoadAttempted)
            {
                return _getFileMetadataForPath;
            }

            _nativeFileMetadataLoadAttempted = true;
            if (string.IsNullOrEmpty(_nativeFileMetadataLibraryPath) || !File.Exists(_nativeFileMetadataLibraryPath))
            {
                return null;
            }

            try
            {
                _nativeFileMetadataLibraryHandle = NativeLibrary.Load(_nativeFileMetadataLibraryPath);
                if (!NativeLibrary.TryGetExport(_nativeFileMetadataLibraryHandle, "GetFileMetadataForPath", out var export))
                {
                    return null;
                }

                _getFileMetadataForPath = Marshal.GetDelegateForFunctionPointer<GetFileMetadataForPathDelegate>(export);
                return _getFileMetadataForPath;
            }
            catch
            {
                return null;
            }
        }
    }

    private static string GetNativeFileMetadataLibraryPath(string tracerHome)
    {
        if (string.IsNullOrEmpty(tracerHome))
        {
            return null;
        }

        var nativeTracerFileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                                       ? "Datadog.Tracer.Native.dylib"
                                       : "Datadog.Tracer.Native.so";
        var nativeTracerDirectory = GetNativeTracerDirectory();
        return nativeTracerDirectory is null
                   ? null
                   : Path.Combine(tracerHome, nativeTracerDirectory, nativeTracerFileName);
    }

    private static string GetNativeTracerDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            string.Equals(FrameworkDescription.Instance.OSPlatform, OSPlatformName.MacOS, StringComparison.Ordinal) ||
            FrameworkDescription.Instance.OSDescription.StartsWith("Darwin", StringComparison.OrdinalIgnoreCase))
        {
            return "osx";
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return null;
        }

        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => Utils.IsAlpine() ? "linux-musl-x64" : "linux-x64",
            Architecture.Arm64 => Utils.IsAlpine() ? "linux-musl-arm64" : "linux-arm64",
            _ => null
        };
    }

#endif

    /// <summary>
    /// Calls POSIX mkdir with the requested mode.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    /// <param name="mode">The requested POSIX mode.</param>
    /// <returns>Zero on success; otherwise a non-zero result with errno available through <c>Marshal.GetLastWin32Error()</c>.</returns>
    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int Mkdir(string path, uint mode);

    /// <summary>
    /// Gets the current effective user id.
    /// </summary>
    /// <returns>The current effective user id.</returns>
    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();

    /// <summary>
    /// Represents the POSIX metadata needed to validate a directory.
    /// </summary>
    /// <param name="Mode">The raw mode bits returned by stat.</param>
    /// <param name="UserId">The owner user id returned by stat.</param>
    private readonly record struct PosixDirectoryInfo(uint Mode, uint UserId);
}
