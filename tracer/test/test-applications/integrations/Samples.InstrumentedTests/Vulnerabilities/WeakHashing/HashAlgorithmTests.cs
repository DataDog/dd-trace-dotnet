// <copyright file="HashAlgorithmTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakHashing;

public class HashAlgorithmTests : InstrumentationTestsBase
{
    private readonly Mock<HashAlgorithm> hashMock = new Mock<HashAlgorithm>();

    [Fact]
    public void GivenAHashAlgorithm_WhenComputeHash_NotVulnerable()
    {
        hashMock.Object.ComputeHash(new byte[] { 3, 5 });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAHashAlgorithm_WhenComputeHash_NotVulnerable2()
    {
        hashMock.Object.ComputeHash(new byte[] { 3, 5 }, 0, 2);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAHashAlgorithm_WhenComputeHash_NotVulnerable3()
    {
        hashMock.Object.ComputeHash(new MemoryStream(100));
        AssertNotVulnerable();
    }

#if NETFRAMEWORK
    [Fact]
    public void GivenAHMACRIPEMD160_WhenComputeHash_NotVulnerable()
    {
        new HMACRIPEMD160(new byte[] { 4, 4 }).ComputeHash(new MemoryStream(100));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenRIPEMD160Managed_WhenComputeHash_NotVulnerable()
    {
        RIPEMD160Managed.Create().ComputeHash(new MemoryStream(100));
        AssertNotVulnerable();
    }
#endif
}
