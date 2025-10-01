// <copyright file="StartupNetFrameworkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using Datadog.Trace.ClrProfiler.Managed.Loader;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.Managed.Loader
{
    public class StartupNetFrameworkTests
    {
        [Fact]
        public void ComputeTfmDirectory_WithTracerHomeDirectory_ReturnsCorrectTfm()
        {
            const string tracerHome = @"C:\path\to\tracer";
            const string expectedDirectory = @"C:\path\to\tracer\net461";

            Startup.ComputeTfmDirectory(tracerHome).Should().Be(expectedDirectory);
        }
    }
}

#endif
