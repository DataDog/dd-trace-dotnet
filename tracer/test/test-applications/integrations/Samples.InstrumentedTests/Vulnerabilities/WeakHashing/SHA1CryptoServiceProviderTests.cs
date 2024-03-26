// <copyright file="SHA1CryptoServiceProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakHashing;

#pragma warning disable SYSLIB0021 // Type or member is obsolete
public class SHA1CryptoServiceProviderTests : InstrumentationTestsBase
{
    [Fact]
    public void GivenASHA1CryptoServiceProvider_WhenCreating_VulnerabilityIsNotLogged()
    {
        _ = new SHA1CryptoServiceProvider();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenASHA1CryptoServiceProvider_WhenCreating_VulnerabilityIsLogged()
    {
        _ = new SHA1CryptoServiceProvider().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable(WeakHashVulnerabilityType, "SHA1", false);
    }

    [Fact]
    public void GivenASHA1CryptoServiceProvider_WhenCreating_VulnerabilityIsLogged2()
    {
        _ = new SHA1CryptoServiceProvider().ComputeHash(new byte[] { 5, 5, 5 });
        AssertVulnerable(WeakHashVulnerabilityType, "SHA1", false);
    }

    [Fact]
    public void GivenASHA1CryptoServiceProvider_WhenCreating_VulnerabilityIsLogged3()
    {
        _ = new SHA1CryptoServiceProvider().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable(WeakHashVulnerabilityType, "SHA1", false);
    }

    [Fact]
    public void SHA1CryptoServiceProvider()
    {
        var crypto = new SHA1CryptoServiceProvider();
        Assert.NotNull(crypto);
        crypto.ComputeHash(new byte[] { 0xFF });
        AssertVulnerable(WeakHashVulnerabilityType, "SHA1", false);
    }
}
