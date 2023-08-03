// <copyright file="ImmutableGCPFunctionSettingsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableGCPFunctionSettingsTests : SettingsTestsBase
    {
        [Fact]
        public void GetIsGCPFunctionTrueWhenDeprecatedFunctionsEnvVarsExist()
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey, "value"),
                (ConfigurationKeys.GCPFunction.DeprecatedProjectKey, "value"));

            var settings = new ImmutableGCPFunctionSettings(source, NullConfigurationTelemetry.Instance);
            settings.IsDeprecatedFunction.Should().BeTrue();
            settings.IsGCPFunction.Should().BeTrue();
            settings.IsNewerFunction.Should().BeFalse();
        }

        [Fact]
        public void GetIsGCPFunctionTrueWhenNonDeprecatedFunctionsEnvVarsExist()
        {
            var source = CreateConfigurationSource(
                (ConfigurationKeys.GCPFunction.FunctionNameKey, "value"),
                (ConfigurationKeys.GCPFunction.FunctionTargetKey, "value"));

            var settings = new ImmutableGCPFunctionSettings(source, NullConfigurationTelemetry.Instance);
            settings.IsNewerFunction.Should().BeTrue();
            settings.IsGCPFunction.Should().BeTrue();
            settings.IsDeprecatedFunction.Should().BeFalse();
        }

        [Fact]
        public void GetIsGCPFunctionFalseWhenNoFunctionsEnvVars()
        {
            var settings = new ImmutableGCPFunctionSettings(CreateConfigurationSource(), NullConfigurationTelemetry.Instance);

            settings.IsNewerFunction.Should().BeFalse();
            settings.IsDeprecatedFunction.Should().BeFalse();
            settings.IsGCPFunction.Should().BeFalse();
        }
    }
}
