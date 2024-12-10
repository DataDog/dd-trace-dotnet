// <copyright file="ProcessHelper.Children.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// Add methods to get the children of a process
/// </summary>
[SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "PInvokes are grouped at the bottom of the class")]
public partial class ProcessHelper
{
    public static IReadOnlyList<int> GetChildrenIds(int parentId)
    {
        var childPids = new List<int>();

        try
        {
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                int ppid;
                try
                {
                    ppid = GetParentProcessId(process);
                }
                catch
                {
                    continue; // Skip processes that can't be accessed
                }

                var id = process.Id;
                if (ppid == parentId && id != parentId)
                {
                    childPids.Add(id);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving child processes: {ex.Message}");
        }

        return childPids;
    }

    private static int GetParentProcessId(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetParentProcessIdWindows(process.Id);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetParentProcessIdLinux(process.Id);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetParentProcessIdMacOS(process.Id);
        }

        throw new PlatformNotSupportedException("Unsupported platform.");
    }

    private static int GetParentProcessIdWindows(int pid)
    {
        try
        {
            var pbi = new PROCESS_BASIC_INFORMATION();
            uint returnLength;

            var hProcess = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, pid);
            if (hProcess == IntPtr.Zero)
            {
                throw new Exception("Could not open process.");
            }

            var status = NtQueryInformationProcess(
                hProcess, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out returnLength);

            CloseHandle(hProcess);

            if (status != 0)
            {
                throw new Exception("NtQueryInformationProcess failed.");
            }

            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting parent PID for process {pid}: {ex.Message}");
        }
    }

    private static int GetParentProcessIdLinux(int pid)
    {
        try
        {
            var statusPath = $"/proc/{pid}/status";
            if (!File.Exists(statusPath))
            {
                throw new Exception("PPid not found.");
            }

            foreach (var line in File.ReadLines(statusPath))
            {
                if (!line.StartsWith("PPid:"))
                {
                    continue;
                }

                if (int.TryParse(line.Substring(5).Trim(), out var ppid))
                {
                    return ppid;
                }
            }

            throw new Exception("PPid not found.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error reading /proc/{pid}/status: {ex.Message}");
        }
    }

    private static int GetParentProcessIdMacOS(int pid)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = $"-o ppid= -p {pid}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(startInfo);
            var output = proc!.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (int.TryParse(output.Trim(), out var ppid))
            {
                return ppid;
            }

            throw new Exception("Failed to parse PPid.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error executing ps command: {ex.Message}");
        }
    }

    // P/Invoke declarations for Windows
    [Flags]
    private enum ProcessAccessFlags : uint
    {
        QueryLimitedInformation = 0x1000
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        uint processInformationLength,
        out uint returnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(
        ProcessAccessFlags processAccess,
        bool bInheritHandle,
        int processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Keeping the original windows struct name")]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved20;
        public IntPtr Reserved21;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }
}
