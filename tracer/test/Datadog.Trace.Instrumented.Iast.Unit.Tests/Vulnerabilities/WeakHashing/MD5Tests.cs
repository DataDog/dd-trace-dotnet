// <copyright file="MD5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.Cryptography;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Settings;
using Moq;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities.WeakHashing;

public class MD5Tests
{
    [Fact]
    public void GivenAMD5_WhenComputeHash_VulnerabilityIsLogged()
    {
        var path = Environment.GetEnvironmentVariable("COR_PROFILER_PATH");
        var home = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
        MD5.Create().ComputeHash(new Mock<Stream>().Object);
        var tracer = Tracer.Instance;
        var iast = Trace.Iast.Iast.Instance;
        AssertVulnerable();
    }

    private void AssertVulnerable(int vulnerabilities = 1)
    {
        Assert.Equal(vulnerabilities, HashBasedDeduplication.Instance.Count);
    }
}
