// <copyright file="IastInstrumentationUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class IastInstrumentationUnitTests : TestHelper
{
    public IastInstrumentationUnitTests(ITestOutputHelper output)
        : base("InstrumentedTests", output)
    {
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestInstrumentedUnitTests()
    {
        bool net462x86 = !Environment.Is64BitProcess && !EnvironmentHelper.IsCoreClr();
        if (!net462x86)
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            {
                EnableIast(true);
                string arguments = string.Empty;
#if NET462
                arguments = @" /Framework:"".NETFramework,Version=v4.6.2"" ";
#endif
                ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent, arguments: arguments);
                processResult.StandardError.Should().BeEmpty("arguments: " + arguments + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
            }
        }
    }
}
