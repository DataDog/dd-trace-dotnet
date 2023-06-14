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
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey, "function_name");
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedProjectKey, "project_1");

            Assert.True(ImmutableGCPFunctionSettings.GetIsGCPFunction());

            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedProjectKey, null);
        }

        [Fact]
        public void GetIsGCPFunctionTrueWhenNonDeprecatedFunctionsEnvVarsExist()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionNameKey, "function_name");
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionTargetKey, "function_target");

            Assert.True(ImmutableGCPFunctionSettings.GetIsGCPFunction());

            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionNameKey, null);
            Environment.SetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionTargetKey, null);
        }

        [Fact]
        public void GetIsGCPFunctionFalseWhenNoFunctionsEnvVars()
        {
            Assert.False(ImmutableGCPFunctionSettings.GetIsGCPFunction());
        }
    }
}
