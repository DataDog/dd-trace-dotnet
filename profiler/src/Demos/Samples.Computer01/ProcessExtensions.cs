// <copyright file="ProcessExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Samples.Computer01
{
    internal static class ProcessExtensions
    {
        public static int SendSignal(this Process process, int sig)
        {
            return SendSignal(process.Id, sig);
        }

        [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
        private static extern int SendSignal(int pid, int sig);
    }
}

#endif
