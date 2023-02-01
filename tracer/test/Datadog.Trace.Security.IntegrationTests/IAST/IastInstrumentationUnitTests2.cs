// <copyright file="IastInstrumentationUnitTests2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class IastInstrumentationUnitTests2 : TestHelper
{
    public IastInstrumentationUnitTests2(ITestOutputHelper output)
        : base("XUnitTests", output)
    {
        SetServiceName("xunit-tests");
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public void TestInstrumentedUnitTests222()
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");

        using (var agent = EnvironmentHelper.GetMockAgent())
        {
            using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent))
            {
                processResult.StandardError.Should().BeEmpty("arguments: " + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestInstrumentedUnitTests333()
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");

        using (var agent = EnvironmentHelper.GetMockAgent())
        {
            using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent))
            {
                processResult.StandardError.Should().BeEmpty("arguments: " + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    public void TestInstrumentedUnitTests444()
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");

        using (var agent = EnvironmentHelper.GetMockAgent())
        {
            using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent))
            {
                processResult.StandardError.Should().BeEmpty("arguments: " + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
            }
        }
    }

    [SkippableFact]
    public void TestInstrumentedUnitTests555()
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");

        using (var agent = EnvironmentHelper.GetMockAgent())
        {
            using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent))
            {
                processResult.StandardError.Should().BeEmpty("arguments: " + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
            }
        }
    }

    [Trait("Category", "TestIntegrations")]
    [SkippableFact]
    public void TestInstrumentedUnitTests6666()
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Logs, "1");

        using (var agent = EnvironmentHelper.GetMockAgent())
        {
            using (ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent))
            {
                processResult.StandardError.Should().BeEmpty("arguments: " + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
            }
        }
    }
}
