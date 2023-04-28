// <copyright file="HMACSHA1Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakHashing;

#pragma warning disable SYSLIB0007 // Type or member is obsolete
public class HMACSHA1Tests : InstrumentationTestsBase
{
    [Fact]
    public void GivenAHMACSHA1_WhenCreating_VulnerabilityIsNotLogged()
    {
#if !NETFRAMEWORK
        Assert.Throws<PlatformNotSupportedException>(() => HMACSHA1.Create());
#else
        HMACSHA1.Create();
        AssertNotVulnerable();
#endif
    }

    [Fact]
    public void GivenAHMACSHA1_WhenCreatingAlgortihm_VulnerabilityIsNotLogged()
    {
#pragma warning disable SYSLIB0045 // Type or member is obsolete
        HMACSHA1.Create("alg");
#pragma warning restore SYSLIB0045 // Type or member is obsolete
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAHMACSHA1_WhenComputeHash_VulnerabilityIsLogged()
    {
        new HMACSHA1().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable(WeakHashVulnerabilityType, "HMACSHA1", false);
    }

    [Fact]
    public void GivenAHMACSHA1_WhenComputeHash_VulnerabilityIsLogged2()
    {
        new HMACSHA1().ComputeHash(new byte[] { });
        AssertVulnerable(WeakHashVulnerabilityType, "HMACSHA1", false);
    }

    [Fact]
    public void GivenAHMACSHA1_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => new HMACSHA1().ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenAHMACSHA1_WhenComputeHash_ExceptionIsThrown2()
    {
        Assert.Throws<NullReferenceException>(() => new HMACSHA1().ComputeHash((Stream)null));
    }

    [Fact]
    public void GivenAHMACreateSHA1_WhenComputeHash_VulnerabilityIsLogged()
    {
#if NETFRAMEWORK
        HMAC.Create().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable(WeakHashVulnerabilityType, "HMACSHA1", false);
#else
        Assert.Throws<PlatformNotSupportedException>(() => HMAC.Create().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2));
#endif
    }

    [Fact]
    public void GivenANewHMACSHA384_WhenComputeHash_VulnerabilityIsNotLogged()
    {
        new HMACSHA384().ComputeHash(new Mock<Stream>().Object);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANewHMACSHA384_WhenComputeHash_VulnerabilityIsNotLogged2()
    {
        new HMACSHA384().ComputeHash(new byte[] { });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenANewHMACSHA384_WhenComputeHash_VulnerabilityIsNotLogged3()
    {
        new HMACSHA384().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertNotVulnerable();
    }
}
