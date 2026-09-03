// <copyright file="ThirdPartyModulesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.ThirdParty;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.ThirdParty;

public class ThirdPartyModulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Contains_HandlesNullOrEmpty(string module)
    {
        ThirdPartyModules.Contains(module).Should().BeTrue();
    }

    [Theory]
    [InlineData("ABCpdf")]
    [InlineData("Akka.Cluster")]
    [InlineData("Datadog.Trace")]
    [InlineData("Datadog.Trace.ClrProfiler.Managed.Loader")]
    public void Contains_HandlesKnownModules(string module)
    {
        ThirdPartyModules.Contains(module).Should().BeTrue();
    }

    [Theory]
    [InlineData("NotAModule")]
    [InlineData("Datadog.Trace.IDontExist")]
    [InlineData("datadog.trace")] // wrong casing
    public void Contains_HandlesUnknownModules(string module)
    {
        ThirdPartyModules.Contains(module).Should().BeFalse();
    }
}
