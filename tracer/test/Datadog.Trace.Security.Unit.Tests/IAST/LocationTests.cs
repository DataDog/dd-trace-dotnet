// <copyright file="LocationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST;

public class LocationTests
{
    [Fact]
    public void GivenALocation_WhenCreatedFromFile_PathIsStored()
    {
        var location = new Location("c:\\mydir\\file.cs", 23, 4);
        location.Method.Should().BeNull();
        location.Path.Should().Be("file.cs");
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored()
    {
        var method = "Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests::<GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable>b__6_0";
        var location = new Location(method, 23, 4);
        location.Path.Should().BeNull();
        location.Method.Should().Be(method);
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromNull_NothingIsStored()
    {
        var location = new Location(null, 23, 4);
        location.Path.Should().BeNull();
        location.Method.Should().BeNull();
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromEmpty_NothingIsStored()
    {
        var location = new Location(string.Empty, 23, 4);
        location.Path.Should().BeNull();
        location.Method.Should().BeNull();
    }
}
