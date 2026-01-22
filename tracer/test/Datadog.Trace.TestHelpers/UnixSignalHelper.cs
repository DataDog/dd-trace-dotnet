// <copyright file="UnixSignalHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.TestHelpers
{
    public static class UnixSignalHelper
    {
        private const int SigTerm = 15;

        public static void SendSigTerm(int pid)
        {
            if (EnvironmentTools.IsWindows())
            {
                throw new PlatformNotSupportedException("SIGTERM is not supported on Windows.");
            }

            if (Kill(pid, SigTerm) != 0)
            {
                var errno = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"kill({pid}, SIGTERM) failed with errno {errno}.");
            }
        }

        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        private static extern int Kill(int pid, int sig);
    }
}
