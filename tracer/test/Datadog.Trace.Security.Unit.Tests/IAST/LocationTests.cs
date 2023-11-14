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
        var location = new Location("/mydir/file.cs", null, 23, 4, null);
        location.Method.Should().BeNull();
        location.Path.Should().Be("file.cs");
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored()
    {
        var method = "GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var typeName = "Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests";
        var location = new Location(null, method, 23, 4, typeName);
        location.Path.Should().Be(typeName);
        location.Method.Should().Be(method);
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored2()
    {
        var method = "GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var location = new Location(null, method, null, 4, null);
        location.Path.Should().BeNull();
        location.Method.Should().Be("GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable");
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromMethod_MethodIsStored3()
    {
        var method = "Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests.GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable";
        var location = new Location(null, method, 23, 4, null);
        location.Path.Should().BeNull();
        location.Method.Should().Be("Samples.InstrumentedTests.Iast.Vulnerabilities.CommandInjectionTests.GivenAProcess_WhenStartTaintedProcess_ThenIsVulnerable");
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromNull_NothingIsStored()
    {
        var location = new Location(null, null, 23, 4, null);
        location.Path.Should().BeNull();
        location.Method.Should().BeNull();
    }

    [Fact]
    public void GivenALocation_WhenCreatedFromFile_FileIsStored3()
    {
        var file = "C:\\Repositories\\code\\specifications\\stock-api\\target\\generated-sources\\openapi\\src\\gen\\csharp\\Stock\\Client\\ApiClient.cs";
        var location = new Location(file, null, 432, 4, null);
        location.Path.Should().Be("ApiClient.cs");
    }
}
