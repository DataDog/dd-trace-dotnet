// <copyright file="MD5CryptoServiceProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakHashing;

#pragma warning disable SYSLIB0021 // Type or member is obsolete

public class MD5CryptoServiceProviderTests : InstrumentationTestsBase
{
    [Fact]
    public void GivenAMD5CryptoServiceProvider_WhenCreating_VulnerabilityIsNotLogged()
    {
        new MD5CryptoServiceProvider();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAMD5CryptoServiceProvider_WhenCreating_VulnerabilityIsLogged()
    {
        new MD5CryptoServiceProvider().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable(WeakHashVulnerabilityType, "MD5", false);
    }

    [Fact]
    public void GivenAMD5CryptoServiceProvider_WhenCreating_VulnerabilityIsLogged2()
    {
        new MD5CryptoServiceProvider().ComputeHash(new byte[] { 5, 5, 5 });
        AssertVulnerable(WeakHashVulnerabilityType, "MD5", false);
    }

    [Fact]
    public void GivenAMD5CryptoServiceProvider_WhenCreating_VulnerabilityIsLogged3()
    {
        new MD5CryptoServiceProvider().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable(WeakHashVulnerabilityType, "MD5", false);
    }
}
