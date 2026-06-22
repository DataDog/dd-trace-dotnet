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

namespace Datadog.Trace.Tools.Runner
{
    internal static class PosixDirectoryAccess
    {
        private const uint PosixDirectoryFileType = 0x4000;
        private const uint PosixFileTypeMask = 0xF000;
        private const uint PosixGroupOrOtherWrite = 0x12; // 022
        private const uint PrivateDirectoryMode = 448; // 0700

        internal static void CreatePrivateDirectory(string path)
        {
            var result = Mkdir(path, PrivateDirectoryMode);
            if (result != 0 && !Directory.Exists(path))
            {
                throw new IOException($"Unable to create directory '{path}'. errno: {Marshal.GetLastWin32Error()}");
            }
        }

        internal static void ValidateDirectoryAccess(string path, bool requireCurrentUserOwner, bool allowGroupOrOtherWrite)
        {
            var directoryInfo = GetDirectoryInfo(path);
            if ((directoryInfo.Mode & PosixFileTypeMask) != PosixDirectoryFileType)
            {
                throw new IOException($"Path '{path}' must be a directory.");
            }

            if (requireCurrentUserOwner && directoryInfo.UserId != GetEffectiveUserId())
            {
                throw new IOException($"Directory '{path}' must be owned by the current user.");
            }

            if (!allowGroupOrOtherWrite && (directoryInfo.Mode & PosixGroupOrOtherWrite) != 0)
            {
                throw new IOException($"Directory '{path}' must not be writable by group or other users.");
            }
        }

        private static PosixDirectoryInfo GetDirectoryInfo(string path)
        {
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

        [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
        private static extern int Mkdir(string path, uint mode);

        [DllImport("libc", EntryPoint = "geteuid")]
        private static extern uint GetEffectiveUserId();

        private readonly record struct PosixDirectoryInfo(uint Mode, uint UserId);
    }
}
