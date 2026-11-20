// <copyright file="TestOptimizationRuntimeTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(TracerInstanceTestCollection))]
public class TestOptimizationRuntimeTagsTests : SettingsTestsBase
{
    private const string DetectedOSVersion = "detected-os-version";

    [Fact]
    public void ReportsDetectedValuesWithoutOverrides()
    {
        var tags = TestOptimizationRuntimeTags.Create(CreateTestOptimization(runtimeTags: null).Object);

        var framework = FrameworkDescription.Instance;
        tags.OSPlatform.Should().Be(framework.OSPlatform);
        tags.OSVersion.Should().Be(DetectedOSVersion);
        tags.OSArchitecture.Should().Be(framework.OSArchitecture);
        tags.RuntimeName.Should().Be(framework.Name);
        tags.RuntimeVersion.Should().Be(framework.ProductVersion);
        tags.RuntimeArchitecture.Should().Be(framework.ProcessArchitecture);
    }

    [Fact]
    public void AppliesOverridesAndKeepsOtherDetectedValues()
    {
        var tags = TestOptimizationRuntimeTags.Create(CreateTestOptimization("{\"os.version\":\"6.12.0\",\"runtime.version\":\"8.0.0\"}").Object);

        var framework = FrameworkDescription.Instance;
        tags.OSVersion.Should().Be("6.12.0");
        tags.RuntimeVersion.Should().Be("8.0.0");
        tags.OSPlatform.Should().Be(framework.OSPlatform);
        tags.RuntimeName.Should().Be(framework.Name);
    }

    [Fact]
    public void TestModuleReportsOverriddenValues()
    {
        var testOptimization = CreateTestOptimization("{\"os.version\":\"6.12.0\",\"runtime.version\":\"8.0.0\"}");
        testOptimization.Setup(x => x.IsRunning).Returns(true);
        testOptimization.Setup(x => x.Log).Returns(DatadogLogging.GetLoggerFor<TestOptimizationRuntimeTagsTests>());
        testOptimization.Setup(x => x.RunId).Returns(Guid.NewGuid().ToString("N"));
        testOptimization.Setup(x => x.CIValues).Returns(CIEnvironmentValues.Instance);
        var previousTestOptimization = TestOptimization.Instance;
        TestOptimization.Instance = testOptimization.Object;
        TestSession? session = null;
        TestModule? module = null;
        try
        {
            session = TestSession.GetOrCreate("dotnet test", workingDirectory: null, framework: "xunit", startDate: null);
            module = session.CreateModule("runtime-tags");
            module.Tags.OSVersion.Should().Be("6.12.0");
            module.Tags.RuntimeVersion.Should().Be("8.0.0");
            module.Tags.OSPlatform.Should().Be(FrameworkDescription.Instance.OSPlatform);
        }
        finally
        {
            module?.Close();
            session?.Close(TestStatus.Pass);
            TestOptimization.Instance = previousTestOptimization;
        }
    }

    private Mock<ITestOptimization> CreateTestOptimization(string? runtimeTags)
    {
        var settings = new TestOptimizationSettings(
            CreateConfigurationSource((ConfigurationKeys.CIVisibility.TestOptimizationRuntimeTags, runtimeTags)),
            NullConfigurationTelemetry.Instance);
        var hostInfo = new Mock<ITestOptimizationHostInfo>();
        hostInfo.Setup(x => x.GetOperatingSystemVersion()).Returns(DetectedOSVersion);
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.Setup(x => x.Settings).Returns(settings);
        testOptimization.Setup(x => x.HostInfo).Returns(hostInfo.Object);
        return testOptimization;
    }
}
