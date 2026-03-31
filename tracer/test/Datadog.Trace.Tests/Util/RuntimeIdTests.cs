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
    [EnvironmentRestorer(ConfigurationKeys.Telemetry.RootSessionId)]
    public class RuntimeIdTests
    {
        [Fact]
        public void RootSessionId_UsesRuntimeIdWhenNotInherited_AndInheritsWhenSet()
        {
            try
            {
                // When no env var is set, root session ID should default to runtime ID
                RuntimeId.ResetForTests();
                Environment.SetEnvironmentVariable(ConfigurationKeys.Telemetry.RootSessionId, null);

                var rootSessionId = RuntimeId.GetRootSessionId();
                rootSessionId.Should().Be(RuntimeId.Get());

                // When env var is pre-set (simulating a child process), root session ID
                // should return the inherited value instead of the current runtime ID
                var inherited = "inherited-root-session-id";
                RuntimeId.ResetForTests();
                Environment.SetEnvironmentVariable(ConfigurationKeys.Telemetry.RootSessionId, inherited);

                RuntimeId.GetRootSessionId().Should().Be(inherited);
                RuntimeId.GetRootSessionId().Should().NotBe(RuntimeId.Get());
            }
            finally
            {
                RuntimeId.ResetForTests();
                Environment.SetEnvironmentVariable(ConfigurationKeys.Telemetry.RootSessionId, null);
                RuntimeId.GetRootSessionId();
            }
        }
    }
}
