// <copyright file="CreateProcess.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Based on https://github.com/dotnet/runtime/blob/045e55abac82e89e0daaee47bcb8433e0fc9ccbc/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Datadog.Trace.TestHelpers.NativeProcess;

public class CreateProcess
{
    private static readonly object CreateProcessLock;

    static CreateProcess()
    {
        // Just making extra sure that the Process static constructor has run
        _ = Process.GetCurrentProcess();

#if NETFRAMEWORK
        const string FieldName = "s_CreateProcessLock";
#else
        const string FieldName = "s_createProcessLock";
#endif

        var createProcessLock = typeof(Process)
            .GetField(FieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(null);

        CreateProcessLock = createProcessLock ?? throw new InvalidOperationException($"Failed to read the {FieldName} field from Process");
    }

    internal static SuspendedProcess StartSuspendedProcess(ProcessStartInfo startInfo)
    {
        // See knowledge base article Q190351 for an explanation of the following code.  Noteworthy tricky points:
        //    * The handles are duplicated as non-inheritable before they are passed to CreateProcess so
        //      that the child process can not close them
        //    * CreateProcess allows you to redirect all or none of the standard IO handles, so we use
        //      GetStdHandle for the handles that are not being redirected

        var commandLine = BuildCommandLine(startInfo.FileName, startInfo.Arguments);

        NativeMethods.STARTUPINFO startupInfo = default;
        NativeMethods.PROCESS_INFORMATION processInfo = default;
        NativeMethods.SECURITY_ATTRIBUTES unused_SecAttrs = default;
        var processHandle = new SafeProcessHandle(IntPtr.Zero, true);
        var threadHandle = new SafeThreadHandle();

        // handles used in parent process
        SafeFileHandle? parentInputPipeHandle = null;
        SafeFileHandle? childInputPipeHandle = null;
        SafeFileHandle? parentOutputPipeHandle = null;
        SafeFileHandle? childOutputPipeHandle = null;
        SafeFileHandle? parentErrorPipeHandle = null;
        SafeFileHandle? childErrorPipeHandle = null;

        // Take a global lock to synchronize all redirect pipe handle creations and CreateProcess
        // calls. We do not want one process to inherit the handles created concurrently for another
        // process, as that will impact the ownership and lifetimes of those handles now inherited
        // into multiple child processes.
        lock (CreateProcessLock)
        {
            try
            {
                startupInfo.cb = Marshal.SizeOf<NativeMethods.STARTUPINFO>();

                // set up the streams
                if (startInfo.RedirectStandardInput || startInfo.RedirectStandardOutput || startInfo.RedirectStandardError)
                {
                    if (startInfo.RedirectStandardInput)
                    {
                        CreatePipe(out parentInputPipeHandle, out childInputPipeHandle, true);
                    }
                    else
                    {
                        childInputPipeHandle = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE), false);
                    }

                    if (startInfo.RedirectStandardOutput)
                    {
                        CreatePipe(out parentOutputPipeHandle, out childOutputPipeHandle, false);
                    }
                    else
                    {
                        childOutputPipeHandle = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE), false);
                    }

                    if (startInfo.RedirectStandardError)
                    {
                        CreatePipe(out parentErrorPipeHandle, out childErrorPipeHandle, false);
                    }
                    else
                    {
                        childErrorPipeHandle = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_ERROR_HANDLE), false);
                    }

                    startupInfo.hStdInput = childInputPipeHandle.DangerousGetHandle();
                    startupInfo.hStdOutput = childOutputPipeHandle.DangerousGetHandle();
                    startupInfo.hStdError = childErrorPipeHandle.DangerousGetHandle();

                    startupInfo.dwFlags = NativeMethods.STARTF_USESTDHANDLES;
                }

                // set up the creation flags parameter
                int creationFlags = NativeMethods.CREATE_SUSPENDED;

                if (startInfo.CreateNoWindow)
                {
                    creationFlags |= NativeMethods.CREATE_NO_WINDOW;
                }

                // set up the environment block parameter
                string? environmentBlock = null;

                if (startInfo.Environment.Count > 0)
                {
                    creationFlags |= NativeMethods.CREATE_UNICODE_ENVIRONMENT;
                    environmentBlock = GetEnvironmentVariablesBlock(startInfo.Environment!);
                }

                var workingDirectory = startInfo.WorkingDirectory;

                if (workingDirectory.Length == 0)
                {
                    workingDirectory = null;
                }

                int errorCode = 0;

                bool retVal = NativeMethods.CreateProcess(
                    null,                // we don't need this since all the info is in commandLine
                    commandLine.ToString(), // command line string
                    ref unused_SecAttrs, // address to process security attributes, we don't need to inherit the handle
                    ref unused_SecAttrs, // address to thread security attributes.
                    true,                // handle inheritance flag
                    creationFlags,       // creation flags
                    environmentBlock, // pointer to new environment block
                    workingDirectory,    // pointer to current directory name
                    ref startupInfo,     // pointer to STARTUPINFO
                    ref processInfo);      // pointer to PROCESS_INFORMATION

                if (!retVal)
                {
                    errorCode = Marshal.GetLastWin32Error();
                }

                if (processInfo.hProcess != IntPtr.Zero && processInfo.hProcess != new IntPtr(-1))
                {
                    processHandle = new(processInfo.hProcess, true);
                }

                if (processInfo.hThread != IntPtr.Zero && processInfo.hThread != new IntPtr(-1))
                {
                    threadHandle = new(processInfo.hThread, true);
                }

                if (!retVal)
                {
                    throw new Win32Exception(errorCode, errorCode is NativeMethods.ERROR_BAD_EXE_FORMAT or NativeMethods.ERROR_EXE_MACHINE_TYPE_MISMATCH ? "Invalid application" : null);
                }
            }
            catch
            {
                parentInputPipeHandle?.Dispose();
                parentOutputPipeHandle?.Dispose();
                parentErrorPipeHandle?.Dispose();
                processHandle.Dispose();
                threadHandle.Dispose();
                throw;
            }
            finally
            {
                childInputPipeHandle?.Dispose();
                childOutputPipeHandle?.Dispose();
                childErrorPipeHandle?.Dispose();
            }
        }

        StreamWriter? standardInput = null;
        StreamReader? standardOutput = null;
        StreamReader? standardError = null;

        if (startInfo.RedirectStandardInput)
        {
            standardInput = new StreamWriter(new FileStream(parentInputPipeHandle!, FileAccess.Write, 4096, false), Console.InputEncoding, 4096);
            standardInput.AutoFlush = true;
        }

        if (startInfo.RedirectStandardOutput)
        {
            var enc = startInfo.StandardOutputEncoding ?? Console.OutputEncoding;
            standardOutput = new StreamReader(new FileStream(parentOutputPipeHandle!, FileAccess.Read, 4096, false), enc, true, 4096);
        }

        if (startInfo.RedirectStandardError)
        {
            var enc = startInfo.StandardErrorEncoding ?? Console.OutputEncoding;
            standardError = new StreamReader(new FileStream(parentErrorPipeHandle!, FileAccess.Read, 4096, false), enc, true, 4096);
        }

        if (processHandle.IsInvalid)
        {
            processHandle.Dispose();
            threadHandle.Dispose();
            throw new Exception("Process creation failed");
        }

        return new SuspendedProcess(startInfo, processInfo.dwProcessId, processHandle, threadHandle, standardInput, standardOutput, standardError);
    }

    private static void CreatePipeWithSecurityAttributes(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, ref NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
    {
        bool ret = NativeMethods.CreatePipe(out hReadPipe, out hWritePipe, ref lpPipeAttributes, nSize);

        if (!ret || hReadPipe.IsInvalid || hWritePipe.IsInvalid)
        {
            throw new Win32Exception();
        }
    }

    // Using synchronous Anonymous pipes for process input/output redirection means we would end up
    // wasting a worker threadpool thread per pipe instance. Overlapped pipe IO is desirable, since
    // it will take advantage of the NT IO completion port infrastructure. But we can't really use
    // Overlapped I/O for process input/output as it would break Console apps (managed Console class
    // methods such as WriteLine as well as native CRT functions like printf) which are making an
    // assumption that the console standard handles (obtained via GetStdHandle()) are opened
    // for synchronous I/O and hence they can work fine with ReadFile/WriteFile synchrnously!
    private static void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs)
    {
        NativeMethods.SECURITY_ATTRIBUTES securityAttributesParent = default;
        securityAttributesParent.bInheritHandle = 1;

        SafeFileHandle? hTmp = null;
        try
        {
            if (parentInputs)
            {
                CreatePipeWithSecurityAttributes(out childHandle, out hTmp, ref securityAttributesParent, 0);
            }
            else
            {
                CreatePipeWithSecurityAttributes(
                    out hTmp,
                    out childHandle,
                    ref securityAttributesParent,
                    0);
            }

            // Duplicate the parent handle to be non-inheritable so that the child process
            // doesn't have access. This is done for correctness sake, exact reason is unclear.
            // One potential theory is that child process can do something brain dead like
            // closing the parent end of the pipe and there by getting into a blocking situation
            // as parent will not be draining the pipe at the other end anymore.
            var currentProcHandle = NativeMethods.GetCurrentProcess();

            if (!NativeMethods.DuplicateHandle(
                currentProcHandle,
                hTmp,
                currentProcHandle,
                out parentHandle,
                0,
                false,
                NativeMethods.DUPLICATE_SAME_ACCESS))
            {
                throw new Win32Exception();
            }
        }
        finally
        {
            if (hTmp != null && !hTmp.IsInvalid)
            {
                hTmp.Dispose();
            }
        }
    }

    private static StringBuilder BuildCommandLine(string executableFileName, string arguments)
    {
        // Construct a StringBuilder with the appropriate command line
        // to pass to CreateProcess.  If the filename isn't already
        // in quotes, we quote it here.  This prevents some security
        // problems (it specifies exactly which part of the string
        // is the file to execute).
        var commandLine = new StringBuilder();
        string fileName = executableFileName.Trim();
        bool fileNameIsQuoted = fileName.StartsWith("\"") && fileName.EndsWith("\"");

        if (!fileNameIsQuoted)
        {
            commandLine.Append('"');
        }

        commandLine.Append(fileName);

        if (!fileNameIsQuoted)
        {
            commandLine.Append('"');
        }

        if (!string.IsNullOrEmpty(arguments))
        {
            commandLine.Append(' ');
            commandLine.Append(arguments);
        }

        return commandLine;
    }

    private static string GetEnvironmentVariablesBlock(IDictionary<string, string> sd)
    {
        // https://docs.microsoft.com/en-us/windows/win32/procthread/changing-environment-variables
        // "All strings in the environment block must be sorted alphabetically by name. The sort is
        //  case-insensitive, Unicode order, without regard to locale. Because the equal sign is a
        //  separator, it must not be used in the name of an environment variable."

        var keys = new string[sd.Count];
        sd.Keys.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.OrdinalIgnoreCase);

        // Join the null-terminated "key=val\0" strings
        var result = new StringBuilder(8 * keys.Length);
        foreach (string key in keys)
        {
            result.Append(key).Append('=').Append(sd[key]).Append('\0');
        }

        return result.ToString();
    }

    private sealed class SafeThreadHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeThreadHandle()
            : base(true)
        {
        }

        internal SafeThreadHandle(IntPtr threadHandle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(threadHandle);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}
