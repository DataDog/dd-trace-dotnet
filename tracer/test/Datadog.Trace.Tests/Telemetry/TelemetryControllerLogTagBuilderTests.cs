// <copyright file="TelemetryControllerLogTagBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

public class TelemetryControllerLogTagBuilderTests
{
    // On .NET 6+, GetLogTags() includes a ",trim:no" suffix (trimming not detected in test environment)
#if NET6_0_OR_GREATER
    private const string TrimSuffix = ",trim:no";
#else
    private const string TrimSuffix = "";
#endif

    [Fact]
    public void TagBuilder_ReturnsExpectedDefaults()
    {
        var builder = new TelemetryController.TagBuilder();
        builder.GetLogTags().Should().Be("ci:0,asm:0,prof:0,dyn:0" + TrimSuffix);
    }

    [Fact]
    public void TagBuilder_UpdateCiVisTag()
    {
        var builder = new TelemetryController.TagBuilder();
        builder.Update(new TestOptimizationSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance), enabled: true);
        builder.GetLogTags().Should().Be("ci:1,asm:0,prof:0,dyn:0" + TrimSuffix);
    }

    [Theory]
    [InlineData((int)TelemetryProductType.AppSec, "ci:0,asm:1,prof:0,dyn:0")]
    [InlineData((int)TelemetryProductType.Profiler, "ci:0,asm:0,prof:1,dyn:0")]
    [InlineData((int)TelemetryProductType.DynamicInstrumentation, "ci:0,asm:0,prof:0,dyn:1")]
    public void TagBuilder_UpdateProductTag(int product, string expected)
    {
        var builder = new TelemetryController.TagBuilder();
        builder.Update((TelemetryProductType)product, enabled: true);
        builder.GetLogTags().Should().Be(expected + TrimSuffix);
    }

    [Fact]
    public void TagBuilder_UpdateAzureAppServicesTag()
    {
        var builder = new TelemetryController.TagBuilder();

        builder.Update(TracerSettings.Create(new()
        {
            { "WEBSITE_SITE_NAME", "site-name" }
        }));

        builder.GetLogTags().Should().Be("ci:0,asm:0,prof:0,dyn:0" + TrimSuffix + ",aas");
    }

    [Fact]
    public void TagBuilder_UpdateAzureFunctionsTag()
    {
        var builder = new TelemetryController.TagBuilder();

        builder.Update(TracerSettings.Create(new()
        {
            { "WEBSITE_SITE_NAME", "site-name" },
            { "FUNCTIONS_EXTENSION_VERSION", "~4" },
            { "FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated" }
        }));

        builder.GetLogTags().Should().Be("ci:0,asm:0,prof:0,dyn:0" + TrimSuffix + ",azf");
    }

    [Fact]
    public void TagBuilder_AddsEverything()
    {
        var builder = new TelemetryController.TagBuilder();
        builder.Update(TracerSettings.Create(new()
        {
            { "WEBSITE_SITE_NAME", "site-name" },
            { "FUNCTIONS_EXTENSION_VERSION", "~4" },
            { "FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated" }
        }));
        builder.Update(TelemetryProductType.Profiler, enabled: true);
        builder.Update(TelemetryProductType.DynamicInstrumentation, enabled: true);
        builder.Update(TelemetryProductType.AppSec, enabled: true);
        builder.Update(new TestOptimizationSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance), enabled: true);
        builder.GetLogTags().Should().Be("ci:1,asm:1,prof:1,dyn:1" + TrimSuffix + ",azf");
    }
}
