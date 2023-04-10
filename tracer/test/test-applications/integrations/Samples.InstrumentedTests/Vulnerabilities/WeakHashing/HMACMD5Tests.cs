// <copyright file="HMACMD5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakHashing;
public class HMACMD5Tests : InstrumentationTestsBase
{
    [Fact]
    public void GivenAHMACMD5_WhenComputeHash_VulnerabilityIsLogged()
    {
        new HMACMD5().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable(WeakHashVulnerabilityType, "HMACMD5", false);
    }

    [Fact]
    public void GivenAHMACMD5_WhenComputeHash_VulnerabilityIsLogged2()
    {
        new HMACMD5().ComputeHash(new byte[] { });
        AssertVulnerable(WeakHashVulnerabilityType, "HMACMD5", false);
    }

    [Fact]
    public void GivenAHMACMD5_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => new HMACMD5().ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenAHMACMD5_WhenComputeHashNullStream_ExceptionIsThrown2()
    {
        Assert.Throws<NullReferenceException>(() => new HMACMD5().ComputeHash((Stream)null));
    }

    [Fact]
    public void GivenAHMACMD5_WhenComputeHash_VulnerabilityIsLogged3()
    {
        new HMACMD5().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable(WeakHashVulnerabilityType, "HMACMD5", false);
    }

    [Fact]
    public void GivenANewHMACMD5_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => new HMACMD5().ComputeHash(new byte[] { 5, 5, 5 }, 10, 2));
    }

#pragma warning disable SYSLIB0045
    [Fact]
    public void GivenAHMACMD5_WhenComputeHashByte3Args_VulnerabilityIsLogged3()
    {
        HMAC.Create("HMACMD5").ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable(WeakHashVulnerabilityType, "HMACMD5", false);
    }

    [Fact]
    public void GivenAHMACMD5_WhenComputeHashStream_VulnerabilityIsLogged()
    {
        HMAC.Create("HMACMD5").ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable(WeakHashVulnerabilityType, "HMACMD5", false);
    }

    [Fact]
    public void GivenAHMACMD5_WhenComputeHashByte_VulnerabilityIsLogged()
    {
        HMAC.Create("HMACMD5").ComputeHash(new byte[] { });
        AssertVulnerable(WeakHashVulnerabilityType, "HMACMD5", false);
    }
#pragma warning restore SYSLIB0045
}
