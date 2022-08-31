// <copyright file="ProcessExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if Linux && NET6_0_OR_GREATER

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Samples.Computer01
{
    internal static class ProcessExtensions
    {
        public static int Kill(this Process process, int sig)
        {
            return SysKill(process.Id, sig);
        }

        [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
        private static extern int SysKill(int pid, int sig);
    }
}

#endif
