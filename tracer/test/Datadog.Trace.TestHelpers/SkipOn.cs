// <copyright file="SkipOn.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;
using Xunit;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// SkipOn helper
/// </summary>
public static class SkipOn
{
    /// <summary>
    /// Platform enum
    /// </summary>
    public enum Platform
    {
        /// <summary>
        /// Windows platform
        /// </summary>
        Windows,

        /// <summary>
        /// Linux platform
        /// </summary>
        Linux,

        /// <summary>
        /// MacOs platform
        /// </summary>
        MacOs
    }

    /// <summary>
    /// Architecture enum
    /// </summary>
    public enum Architecture
    {
        /// <summary>
        /// X86 arch
        /// </summary>
        X86,

        /// <summary>
        /// X64 arch
        /// </summary>
        X64,

        /// <summary>
        /// ARM64 arch
        /// </summary>
        ARM64
    }

    public static void PlatformAndArchitecture(Platform platform, Architecture architecture)
    {
#if NETCOREAPP
        if ((platform == Platform.Linux && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
            (platform == Platform.Windows && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ||
            (platform == Platform.MacOs && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
        {
            if ((architecture == Architecture.X64 && RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.X64) ||
                (architecture == Architecture.X86 && RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.X86) ||
                (architecture == Architecture.ARM64 && RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64))
            {
                throw new SkipException($"Platform {platform} with Architecture {architecture} is not supported by this test.");
            }
        }
#endif
    }
}
