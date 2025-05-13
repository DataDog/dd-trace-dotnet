// <copyright file="LocationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST;

public class LocationTests
{
    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored()
    {
        var method = "GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var typeName = "Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests";
        var location = new Location(typeName, method, 23, 4);
        location.Path.Should().Be(typeName);
        location.Method.Should().Be(method);
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored2()
    {
        var method = "GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var location = new Location(null, method, null, 4);
        location.Path.Should().BeNull();
        location.Method.Should().Be("GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable");
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored3()
    {
        var method = "Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests.GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var location = new Location(null, method, 23, 4);
        location.Path.Should().BeNull();
        location.Method.Should().Be("Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests.GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable");
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromNull_NothingIsStored()
    {
        var location = new Location(null, null, 23, 4);
        location.Path.Should().BeNull();
        location.Method.Should().BeNull();
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromStackFrame_ValueIsExpected()
    {
        var stack = new StackTrace();
        var frame = stack.GetFrame(0);
        var location = new Location(frame, stack, null, null);
        location.Path.Should().Be("Datadog.Trace.Security.Unit.Tests.IAST.LocationTests");
        location.Method.Should().Be("GivenALocation_WhenCreatedFromStackFrame_ValueIsExpected");
    }
}
