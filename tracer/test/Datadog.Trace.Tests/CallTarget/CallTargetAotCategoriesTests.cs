// <copyright file="CallTargetAotCategoriesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget;

/// <summary>
/// The integrations of an application instrumented at build time follow their instrumentation categories like the
/// native tracer's definitions: enabled while one of their categories is. The categories are global to the process, so
/// every test uses its own integration types and only the categories of its integrations matter.
/// </summary>
public class CallTargetAotCategoriesTests
{
    [Fact]
    public void IntegrationFollowsItsCategory()
    {
        CallTargetAotCategories.Disable(InstrumentationCategory.AppSec);
        CallTargetAotCategories.Register<AppSecIntegration, Target1>((uint)InstrumentationCategory.AppSec);
        IntegrationOptions<AppSecIntegration, Target1>.IsIntegrationEnabled.Should().BeFalse();

        CallTargetAotCategories.Enable(InstrumentationCategory.AppSec).Should().BeGreaterThan(0);
        IntegrationOptions<AppSecIntegration, Target1>.IsIntegrationEnabled.Should().BeTrue();

        CallTargetAotCategories.Disable(InstrumentationCategory.AppSec);
        IntegrationOptions<AppSecIntegration, Target1>.IsIntegrationEnabled.Should().BeFalse();
    }

    [Fact]
    public void IntegrationIsEnabledWhileOneOfItsCategoriesIs()
    {
        CallTargetAotCategories.Disable(InstrumentationCategory.Iast | InstrumentationCategory.Rasp);
        CallTargetAotCategories.Register<IastRaspIntegration, Target2>((uint)(InstrumentationCategory.Iast | InstrumentationCategory.Rasp));

        CallTargetAotCategories.Enable(InstrumentationCategory.Iast | InstrumentationCategory.Rasp);
        CallTargetAotCategories.Disable(InstrumentationCategory.Rasp);
        IntegrationOptions<IastRaspIntegration, Target2>.IsIntegrationEnabled.Should().BeTrue();

        CallTargetAotCategories.Disable(InstrumentationCategory.Iast);
        IntegrationOptions<IastRaspIntegration, Target2>.IsIntegrationEnabled.Should().BeFalse();
    }

    [Fact]
    public void RegistrationTakesTheEnabledCategories()
    {
        CallTargetAotCategories.Enable(InstrumentationCategory.Tracing);
        CallTargetAotCategories.Register<TracingIntegration, Target3>((uint)InstrumentationCategory.Tracing);
        IntegrationOptions<TracingIntegration, Target3>.IsIntegrationEnabled.Should().BeTrue();
    }

    [Fact]
    public void IntegrationDisabledByAnErrorStaysDisabled()
    {
        CallTargetAotCategories.Enable(InstrumentationCategory.Tracing);
        CallTargetAotCategories.Register<TracingIntegration, Target4>((uint)InstrumentationCategory.Tracing);
        IntegrationOptions<TracingIntegration, Target4>.DisableIntegration();

        CallTargetAotCategories.Disable(InstrumentationCategory.Tracing);
        CallTargetAotCategories.Enable(InstrumentationCategory.Tracing);
        IntegrationOptions<TracingIntegration, Target4>.IsIntegrationEnabled.Should().BeFalse();
    }

    private sealed class AppSecIntegration
    {
    }

    private sealed class IastRaspIntegration
    {
    }

    private sealed class TracingIntegration
    {
    }

    private sealed class Target1
    {
    }

    private sealed class Target2
    {
    }

    private sealed class Target3
    {
    }

    private sealed class Target4
    {
    }
}
