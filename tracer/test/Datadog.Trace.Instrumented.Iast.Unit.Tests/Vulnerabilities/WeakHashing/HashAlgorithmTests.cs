// <copyright file="HashAlgorithmTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System.IO;
using System.Security.Cryptography;
using Moq;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities.WeakHashing;

public class HashAlgorithmTests : InstrumentationTestsBase
{
    private readonly Mock<HashAlgorithm> hashMock = new Mock<HashAlgorithm>();

    [Fact]
    public void GivenAPage_WhenMapPath_Vulnerable()
    {
        hashMock.Object.ComputeHash(new byte[] { 3, 5 });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPage_WhenMapPath_Vulnerable2()
    {
        hashMock.Object.ComputeHash(new byte[] { 3, 5 }, 0, 2);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPage_WhenMapPath_Vulnerable3()
    {
        hashMock.Object.ComputeHash(new MemoryStream(100));
        AssertNotVulnerable();
    }
}

#endif
