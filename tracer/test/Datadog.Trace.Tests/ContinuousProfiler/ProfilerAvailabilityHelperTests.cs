// <copyright file="ProfilerAvailabilityHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Serverless;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using ProfilerAvailabilityHelper = Datadog.Trace.ContinuousProfiler.ProfilerAvailabilityHelper;

namespace Datadog.Trace.Tests.ContinuousProfiler;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(
    ConfigurationKeys.ContinuousProfiler.InternalProfilingNativeEnginePath,
    PlatformKeys.Aws.LambdaFunctionName,
    PlatformKeys.AzureAppService.SiteNameKey,
    PlatformKeys.AzureFunctions.FunctionsWorkerRuntime,
    PlatformKeys.AzureFunctions.FunctionsExtensionVersion,
    ConfigurationKeys.AzureAppService.AzureAppServicesContextKey)]
public class ProfilerAvailabilityHelperTests
{
    private static readonly Func<bool> ClrProfilerIsAttached = () => true;
    private static readonly Func<bool> ClrProfilerNotAttached = () => false;

    [SkippableFact]
    public void IsContinuousProfilerAvailable_OnUnsupportedPlatforms_ReturnsFalse()
    {
        // Skip on platforms that it's available
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X64);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.X64);

        ProfilerAvailabilityHelper.IsContinuousProfilerAvailable_TestingOnly(
            ClrProfilerIsAttached,
            new Aws().IsLambda,
            new Azure().IsFunction).Should().BeFalse();
    }

    [SkippableTheory]
    [PairwiseData]
    public void IsContinuousProfilerAvailable_OnSupportedPlatforms_WithNoEnvVars_ReturnsClrAttached(bool clrAttached)
    {
        SkipUnsupported();
        var attachedCheck = clrAttached ? ClrProfilerIsAttached : ClrProfilerNotAttached;
        ProfilerAvailabilityHelper.IsContinuousProfilerAvailable_TestingOnly(
            attachedCheck,
            new Aws().IsLambda,
            new Azure().IsFunction).Should().Be(clrAttached);
    }

    [SkippableTheory]
    [PairwiseData]
    public void IsContinuousProfilerAvailable_OnWindows_WithEnvVar_IgnoresAttachment_ReturnsTrue(bool clrAttached)
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);
        var attachedCheck = clrAttached ? ClrProfilerIsAttached : ClrProfilerNotAttached;
        Environment.SetEnvironmentVariable("DD_INTERNAL_PROFILING_NATIVE_ENGINE_PATH", @"c:\some\path");
        ProfilerAvailabilityHelper.IsContinuousProfilerAvailable_TestingOnly(
            attachedCheck,
            new Aws().IsLambda,
            new Azure().IsFunction).Should().BeTrue();
    }

    [SkippableTheory]
    [PairwiseData]
    public void IsContinuousProfilerAvailable_OnWindows_NoEnvVar_IgnoresAttachment_ReturnsFalse(bool clrAttached)
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);
        var attachedCheck = clrAttached ? ClrProfilerIsAttached : ClrProfilerNotAttached;
        ProfilerAvailabilityHelper.IsContinuousProfilerAvailable_TestingOnly(
            attachedCheck,
            new Aws().IsLambda,
            new Azure().IsFunction).Should().BeFalse();
    }

    [SkippableFact]
    public void IsContinuousProfilerAvailable_InLambda_IgnoresAttachment_ReturnsFalse()
    {
        SkipUnsupported();
        Environment.SetEnvironmentVariable(PlatformKeys.Aws.LambdaFunctionName, @"SomeFunction");
        ProfilerAvailabilityHelper.IsContinuousProfilerAvailable_TestingOnly(
            ClrProfilerIsAttached,
            new Aws().IsLambda,
            new Azure().IsFunction).Should().BeFalse();
    }

    [SkippableFact]
    public void IsContinuousProfilerAvailable_InAzureFunctions_IgnoresAttachment_ReturnsFalse()
    {
        SkipUnsupported();
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.SiteNameKey, "MyApp");
        Environment.SetEnvironmentVariable(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime, "dotnet");
        Environment.SetEnvironmentVariable(PlatformKeys.AzureFunctions.FunctionsExtensionVersion, "v6.0");
        ProfilerAvailabilityHelper.IsContinuousProfilerAvailable_TestingOnly(
            ClrProfilerIsAttached,
            new Aws().IsLambda,
            new Azure().IsFunction).Should().BeFalse();
    }

    private static void SkipUnsupported()
    {
        SkipOn.Platform(SkipOn.PlatformValue.MacOs);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.X86);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Linux, SkipOn.ArchitectureValue.ARM64);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.ARM64);
        SkipOn.Platform(SkipOn.PlatformValue.Windows); // Windows is controlled by env var only, so doesn't apply to most tests
    }
}
