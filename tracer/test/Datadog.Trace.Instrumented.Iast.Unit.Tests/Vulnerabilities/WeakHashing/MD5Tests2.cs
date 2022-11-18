// <copyright file="MD5Tests2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities.WeakHashing;

public class MD5Tests2 : InstrumentationTestsBase
{
    [Fact]
    public void GivenAMD5_WhenComputeHash_VulnerabilityIsLoggedhhh()
    {
        var rrr = Environment.GetEnvironmentVariable("RRR");
        MD5.Create().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable();
    }
}
