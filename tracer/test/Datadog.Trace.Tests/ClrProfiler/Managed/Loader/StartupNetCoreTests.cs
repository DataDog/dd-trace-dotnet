// <copyright file="StartupNetCoreTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP

using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.ClrProfiler.Managed.Loader;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.Managed.Loader
{
    public class StartupNetCoreTests
    {
        [Fact]
        public void ComputeTfmDirectory_WithTracerHomeDirectory_ReturnsCorrectTfm()
        {
            string tracerHome;
            string expectedTfm;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                tracerHome = @"C:\path\to\tracer";
            }
            else
            {
                tracerHome = "/path/to/tracer";
            }

            // Determine TFM based on Environment.Version, matching the logic in Startup.ComputeTfmDirectory
            var version = Environment.Version;

            if (version.Major >= 6)
            {
                expectedTfm = "net6.0";
            }
            else if (version is { Major: 3, Minor: >= 1 } || version.Major == 5)
            {
                expectedTfm = "netcoreapp3.1";
            }
            else
            {
                expectedTfm = "netstandard2.0";
            }

            var expectedDirectory = Path.Combine(tracerHome, expectedTfm);
            Startup.ComputeTfmDirectory(tracerHome).Should().Be(expectedDirectory);
        }
    }
}

#endif
