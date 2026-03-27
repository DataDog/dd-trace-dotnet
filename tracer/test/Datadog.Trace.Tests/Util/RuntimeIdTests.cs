// <copyright file="RuntimeIdTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class RuntimeIdTests
    {
        [Fact]
        public void RootSessionId_DefaultsToRuntimeId()
        {
            var rootSessionId = RuntimeId.GetRootSessionId();
            rootSessionId.Should().Be(RuntimeId.Get());
        }

        [Fact]
        public void RootSessionId_SetsEnvVar()
        {
            var rootSessionId = RuntimeId.GetRootSessionId();
            Environment.GetEnvironmentVariable(ConfigurationKeys.Telemetry.RootSessionId)
                       .Should().Be(rootSessionId);
        }
    }
}
