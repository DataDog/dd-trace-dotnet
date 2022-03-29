// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Modified excerpt from dotnet/runtime. This version contains only
// the types, methods, and interfaces used by Vendored.System.Diagnostics.DiagnosticSource.Tests.csproj

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Vendored.System
{
    public static class AdminHelpers
    {
        /// <summary>
        /// Runs the given command as sudo (for Unix).
        /// </summary>
        /// <param name="commandLine">The command line to run as sudo</param>
        /// <returns> Returns the process exit code (0 typically means it is successful)</returns>
        public static int RunAsSudo(string commandLine)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "sudo",
                Arguments = commandLine
            };

            using (Process process = Process.Start(startInfo))
            {
                Assert.True(process.WaitForExit(30000));
                return process.ExitCode;
            }
        }
    }
}
