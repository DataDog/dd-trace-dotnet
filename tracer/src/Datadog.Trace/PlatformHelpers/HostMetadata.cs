// <copyright file="HostMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers
{
    internal class HostMetadata
    {
        static HostMetadata()
        {
            TryGetKernelInformation(
                kernel: out var kernel,
                kernelRelease: out var release,
                kernelVersion: out var version);

            Instance = new HostMetadata(
                hostname: GetHostInternal(),
                kernelName: kernel,
                kernelRelease: release,
                kernelVersion: version);
        }

        private HostMetadata(string? hostname, string? kernelName, string? kernelRelease, string? kernelVersion)
        {
            Hostname = hostname;
            KernelName = kernelName;
            KernelRelease = kernelRelease;
            KernelVersion = kernelVersion;
        }

        public static HostMetadata Instance { get; }

        /// <summary>
        /// Gets the name of the host on which the code is running
        /// Returns <c>null</c> if the host name can not be determined
        /// </summary>
        public string? Hostname { get; }

        /// <summary>
        /// Gets the name of the kernel, e.g. Linux
        /// Returns <c>null</c> if it can not be determined
        /// </summary>
        public string? KernelName { get; }

        /// <summary>
        /// Gets the release name of the kernel, e.g. 3.2.0-4-686-pae
        /// Returns <c>null</c> if it can not be determined
        /// </summary>
        public string? KernelRelease { get; }

        /// <summary>
        /// Gets the version number of the kernel, e.g. #1 SMP Debian 3.2.63-2+deb7u2
        /// Returns <c>null</c> if it can not be determined
        /// </summary>
        public string? KernelVersion { get; }

        // internal for testing
        internal static void ParseKernel(string fullVersion, out string? kernel, out string? kernelRelease, out string? kernelVersion)
        {
            kernel = null;
            kernelRelease = null;
            kernelVersion = null;

            if (string.IsNullOrEmpty(fullVersion))
            {
                return;
            }

            var firstWord = fullVersion.IndexOf(value: " ", StringComparison.OrdinalIgnoreCase);
            if (firstWord < 0)
            {
                return;
            }

            kernel = fullVersion.Substring(0, firstWord);

            var releaseStart = fullVersion.IndexOf("version ", StringComparison.OrdinalIgnoreCase);
            if (releaseStart < 0)
            {
                return;
            }

            releaseStart += 8; // "version ".Length

            var releaseEnd = fullVersion.IndexOf(" ", startIndex: releaseStart, StringComparison.OrdinalIgnoreCase);
            if (releaseEnd < 0)
            {
                return;
            }

            kernelRelease = fullVersion.Substring(releaseStart, releaseEnd - releaseStart);

            var versionIndex = fullVersion.LastIndexOf("#", StringComparison.OrdinalIgnoreCase);
            if (versionIndex < releaseEnd)
            {
                return;
            }

            kernelVersion = fullVersion.Substring(versionIndex);
        }

        private static void TryGetKernelInformation(out string? kernel, out string? kernelRelease, out string? kernelVersion)
        {
            try
            {
                if (File.Exists(@"/proc/version"))
                {
                    // e.g. Linux version 5.10.60.1-microsoft-standard-WSL2 (oe-user@oe-host) (x86_64-msft-linux-gcc (GCC) 9.3.0, GNU ld (GNU Binutils) 2.34.0.20200220) #1 SMP Wed Aug 25 23:20:18 UTC 2021
                    var fullVersion = File.ReadAllText("/proc/version");

                    ParseKernel(fullVersion, out kernel, out kernelRelease, out kernelVersion);
                    return;
                }
            }
            catch
            {
                // May be permissions issues etc
            }

            kernel = null;
            kernelRelease = null;
            kernelVersion = null;
        }

        private static string? GetHostInternal()
        {
            try
            {
                var host = EnvironmentHelpers.GetMachineName();
                if (!string.IsNullOrEmpty(host))
                {
                    return host;
                }

                return EnvironmentHelpers.GetEnvironmentVariable("COMPUTERNAME");
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.Security.SecurityException)
            {
                // We may get a security exception looking up the machine name
                // You must have Unrestricted EnvironmentPermission to access resource
            }

            return null;
        }
    }
}
