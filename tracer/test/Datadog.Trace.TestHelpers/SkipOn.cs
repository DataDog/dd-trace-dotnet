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
    public enum PlatformValue
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
    public enum ArchitectureValue
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

    public static void PlatformAndArchitecture(PlatformValue platform, ArchitectureValue architecture)
    {
#if NETCOREAPP
        if ((platform == PlatformValue.Linux && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
            (platform == PlatformValue.Windows && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ||
            (platform == PlatformValue.MacOs && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
        {
            if ((architecture == ArchitectureValue.X64 && RuntimeInformation.ProcessArchitecture == Architecture.X64) ||
                (architecture == ArchitectureValue.X86 && RuntimeInformation.ProcessArchitecture == Architecture.X86) ||
                (architecture == ArchitectureValue.ARM64 && RuntimeInformation.ProcessArchitecture == Architecture.Arm64))
            {
                throw new SkipException($"Platform '{platform}' with Architecture '{architecture}' is not supported by this test.");
            }
        }
#else
        if ((platform == PlatformValue.Linux && FrameworkDescription.Instance.OSPlatform == OSPlatformName.Linux) ||
            (platform == PlatformValue.Windows && FrameworkDescription.Instance.OSPlatform == OSPlatformName.Windows) ||
            (platform == PlatformValue.MacOs && FrameworkDescription.Instance.OSPlatform == OSPlatformName.MacOS))
        {
            if ((architecture == ArchitectureValue.X64 && FrameworkDescription.Instance.ProcessArchitecture == "x64") ||
                (architecture == ArchitectureValue.X86 && FrameworkDescription.Instance.ProcessArchitecture == "x86") ||
                (architecture == ArchitectureValue.ARM64 && FrameworkDescription.Instance.ProcessArchitecture == "arm64"))
            {
                throw new SkipException($"Platform '{platform}' with Architecture '{architecture}' is not supported by this test.");
            }
        }
#endif
    }

    public static void Platform(PlatformValue platform)
    {
#if NETCOREAPP
        if ((platform == PlatformValue.Linux && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
            (platform == PlatformValue.Windows && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ||
            (platform == PlatformValue.MacOs && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
        {
            throw new SkipException($"Platform '{platform}' is not supported by this test.");
        }
#else
        if ((platform == PlatformValue.Linux && FrameworkDescription.Instance.OSPlatform == OSPlatformName.Linux) ||
            (platform == PlatformValue.Windows && FrameworkDescription.Instance.OSPlatform == OSPlatformName.Windows) ||
            (platform == PlatformValue.MacOs && FrameworkDescription.Instance.OSPlatform == OSPlatformName.MacOS))
        {
            throw new SkipException($"Platform '{platform}' is not supported by this test.");
        }
#endif
    }
}
