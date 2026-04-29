// <copyright file="RcmClientTracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RcmClientTracerTests
{
    [Fact]
    public void Create_VersionTag_UsesAppVersion()
    {
        var tracer = RcmClientTracer.Create(
            runtimeId: "runtime-id",
            tracerVersion: "5.55.555", // gets overrode to be the _real_ tracer version
            service: "my-service",
            env: "env",
            appVersion: "1.2.3",
            globalTags: new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
            processTags: null);

        tracer.Tags.Should().Contain("version:1.2.3");
        tracer.Tags.Should().NotContain("version:my-service");
    }

    [Fact]
    public void Create_NoVersionTag_WhenAppVersionIsNull()
    {
        var tracer = RcmClientTracer.Create(
            runtimeId: "runtime-id",
            tracerVersion: "5.55.555", // gets overrode to be the _real_ tracer version
            service: "my-service",
            env: "env",
            appVersion: null,
            globalTags: new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
            processTags: null);

        tracer.Tags.Should().NotContain(t => t.StartsWith("version:"));
    }
}
