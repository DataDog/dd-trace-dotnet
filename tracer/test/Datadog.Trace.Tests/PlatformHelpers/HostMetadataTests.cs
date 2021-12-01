// <copyright file="HostMetadataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.PlatformHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.PlatformHelpers
{
    public class HostMetadataTests
    {
        [Fact]
        public void CanGetHostMetadata()
        {
            HostMetadata.Instance.Hostname.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [MemberData(nameof(TestData.ValidKernels), MemberType = typeof(TestData))]
        public void CanParseKernelVersion(
            string fullVersion,
            string expectedKernel,
            string expectedRelease,
            string expectedVersion)
        {
            HostMetadata.ParseKernel(fullVersion, out var kernel, out var release, out var version);

            using var scope = new AssertionScope();
            kernel.Should().Be(expectedKernel);
            release.Should().Be(expectedRelease);
            version.Should().Be(expectedVersion);
        }

        [Theory]
        [MemberData(nameof(TestData.InvalidKernels), MemberType = typeof(TestData))]
        public void CanParseMalformedKernels(
            string fullVersion,
            string expectedKernel,
            string expectedRelease,
            string expectedVersion)
        {
            HostMetadata.ParseKernel(fullVersion, out var kernel, out var release, out var version);

            using var scope = new AssertionScope();
            kernel.Should().Be(expectedKernel);
            release.Should().Be(expectedRelease);
            version.Should().Be(expectedVersion);
        }

        [Fact]
        public void CanRetrieveKernelInfo()
        {
            if (FrameworkDescription.Instance.OSPlatform == OSPlatform.Linux)
            {
                HostMetadata.Instance.KernelName.Should().NotBeNullOrEmpty();
                HostMetadata.Instance.KernelRelease.Should().NotBeNullOrEmpty();
                HostMetadata.Instance.KernelVersion.Should().NotBeNullOrEmpty();
            }
        }

        public static class TestData
        {
            /// <summary>
            /// Gets /proc/version string, kernel, release, version
            /// </summary>
            public static TheoryData<string, string, string, string> ValidKernels { get; } = new()
            {
                {
                    "Linux version 3.2.0-4-686-pae (debian-kernel@lists.debian.org) (gcc version 4.6.3 (Debian 4.6.3-14) ) #1 SMP Debian 3.2.63-2+deb7u2",
                    "Linux",
                    "3.2.0-4-686-pae",
                    "#1 SMP Debian 3.2.63-2+deb7u2"
                },
                {
                    "Linux version 5.10.60.1-microsoft-standard-WSL2 (oe-user@oe-host) (x86_64-msft-linux-gcc (GCC) 9.3.0, GNU ld (GNU Binutils) 2.34.0.20200220) #1 SMP Wed Aug 25 23:20:18 UTC 2021",
                    "Linux",
                    "5.10.60.1-microsoft-standard-WSL2",
                    "#1 SMP Wed Aug 25 23:20:18 UTC 2021"
                },
                {
                    "Linux version 4.9.32-0-hardened (buildozer@build-3-6-x86_64) (gcc version 6.3.0 (Alpine 6.3.0) ) #1-Alpine SMP Fri Jun 16 12:20:58 GMT 2017",
                    "Linux",
                    "4.9.32-0-hardened",
                    "#1-Alpine SMP Fri Jun 16 12:20:58 GMT 2017"
                },
            };

            /// <summary>
            /// Gets /proc/version string, canBeParsed, kernel, release, version for invalid kernel strings
            /// </summary>
            public static TheoryData<string, string, string, string> InvalidKernels { get; } = new()
            {
                { "MALFORMED", null, null, null },
                { "MALFORMED hjfdshh", "MALFORMED", null, null },
                { "MALFORMED version", "MALFORMED", null, null },
                { "MALFORMED version fhdjkhf", "MALFORMED", null, null },
                { "MALFORMED version fhdjkhf#1", "MALFORMED", null, null },
                { "MALFORMED version fhdjkhf #1", "MALFORMED", "fhdjkhf", "#1" },
                { "MALFORMED version #1", "MALFORMED", null, null },
            };
        }
    }
}
