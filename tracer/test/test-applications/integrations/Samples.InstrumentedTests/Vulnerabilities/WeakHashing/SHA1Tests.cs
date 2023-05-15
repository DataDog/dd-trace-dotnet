// <copyright file="SHA1Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakHashing;

public class SHA1Tests : InstrumentationTestsBase
{
    [Fact]
    public void GivenASHA1_WhenCreating_VulnerabilityIsNotLogged()
    {
        SHA1.Create();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASHA1_WhenCreatingAlgortihm_VulnerabilityIsNotLogged()
    {
#pragma warning disable SYSLIB0045 // Type or member is obsolete
        SHA1.Create("alg");
#pragma warning restore SYSLIB0045 // Type or member is obsolete
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASHA1_WhenCreatingNull_ExceptionIsThrown()
    {
#pragma warning disable SYSLIB0045 // Type or member is obsolete
        Assert.Throws<ArgumentNullException>(() => SHA1.Create(null));
#pragma warning restore SYSLIB0045 // Type or member is obsolete
    }

    [Fact]
    public void GivenASHA1_WhenComputeHash_VulnerabilityIsLogged()
    {
        SHA1.Create().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable(WeakHashVulnerabilityType, "SHA1", false);
    }

    [Fact]
    public void GivenASHA1_WhenComputeHash_VulnerabilityIsLogged2()
    {
        SHA1.Create().ComputeHash(new byte[] { });
        AssertVulnerable(WeakHashVulnerabilityType, "SHA1", false);
    }

    [Fact]
    public void GivenASHA1_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => SHA1.Create().ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenASHA1_WhenComputeHash_ExceptionIsThrown2()
    {
        Assert.Throws<NullReferenceException>(() => SHA1.Create().ComputeHash((Stream)null));
    }

    [Fact]
    public void GivenASHA1_WhenComputeHash_VulnerabilityIsLogged3()
    {
        SHA1.Create().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable(WeakHashVulnerabilityType, "SHA1", false);
    }

    [Fact]
    public void GivenASHA256_WhenComputeHash_VulnerabilityIsNotLogged()
    {
        SHA256.Create().ComputeHash(new byte[] { 5, 5, 5 });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASHA512_WhenComputeHash_VulnerabilityIsNotLogged()
    {
        SHA512.Create().ComputeHash(new byte[] { 5, 5, 5 });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASHA384_WhenComputeHash_VulnerabilityIsLogged()
    {
        SHA384.Create().ComputeHash(new byte[] { 5, 5, 5 });
        AssertNotVulnerable();
    }

#if NETFRAMEWORK
    [Fact]
    public void GivenAMACTripleDES_WhenComputeHash_VulnerabilityIsNotLogged()
    {
        // This is vulnerable because internally, it is using HMACSHA1
        MACTripleDES.Create().ComputeHash(new byte[] { 5, 5, 5 });
        AssertVulnerable(WeakHashVulnerabilityType, "HMACSHA1", false);
    }
#endif
}
