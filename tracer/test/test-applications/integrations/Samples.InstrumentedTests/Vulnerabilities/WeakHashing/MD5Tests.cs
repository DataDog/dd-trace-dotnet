// <copyright file="MD5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakHashing;

public class MD5Tests : InstrumentationTestsBase
{
    [Fact]
    public void GivenAMD5_WhenCreating_VulnerabilityIsNotLogged()
    {
        MD5.Create();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMD5_WhenCreatingAlgortihm_VulnerabilityIsNotLogged()
    {
#pragma warning disable SYSLIB0045 // Type or member is obsolete
        MD5.Create("alg");
#pragma warning restore SYSLIB0045 // Type or member is obsolete
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMD5_WhenCreatingNull_ExceptionIsThrown()
    {
#pragma warning disable SYSLIB0045 // Type or member is obsolete
        Assert.Throws<ArgumentNullException>(() => MD5.Create(null));
#pragma warning restore SYSLIB0045 // Type or member is obsolete
    }

    [Fact]
    public void GivenAMD5_WhenComputeHash_VulnerabilityIsLogged()
    {
        MD5.Create().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable(WeakHashVulnerabilityType, "MD5", false);
    }

    [Fact]
    public void GivenAMD5_WhenComputeHash_VulnerabilityIsLogged2()
    {
        MD5.Create().ComputeHash(new byte[] { });
        AssertVulnerable(WeakHashVulnerabilityType, "MD5", false);
    }

    [Fact]
    public void GivenAMD5_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => MD5.Create().ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenAMD5_WhenComputeHash_ExceptionIsThrown2()
    {
        Assert.Throws<NullReferenceException>(() => MD5.Create().ComputeHash(((Stream)null)));
    }

    [Fact]
    public void GivenAMD5_WhenComputeHash_VulnerabilityIsLogged3()
    {
        MD5.Create().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable(WeakHashVulnerabilityType, "MD5", false);
    }
}
