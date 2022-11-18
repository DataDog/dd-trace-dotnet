// <copyright file="HMACRIPEMD160Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

/*
#if !NET50 && !NET60

using System;
using System.IO;
using Moq;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities.WeakHashing;

public class HMACRIPEMD160Tests : InstrumentationTestsBase
{

#if !NETCORE31 && !NETCORE21

    [Fact]
    public void GivenAHMACRIPEMD160_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => new HMACRIPEMD160().ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenAHMACRIPEMD160_WhenComputeHash_ExceptionIsThrown2()
    {
        Assert.Throws<NullReferenceException>(() => new HMACRIPEMD160().ComputeHash((Stream)null));
    }
#endif

    [Fact]
    public void GivenAHMACRIPEMD160_WhenComputeHash_ExceptionIsThrown3()
    {
        Assert.Throws<ArgumentException>(() => new HMACRIPEMD160(new byte[] { 4, 4 }).ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenAHMACRIPEMD160_WhenComputeHash_ExceptionIsThrown4()
    {
        Assert.Throws<NullReferenceException>(() => new HMACRIPEMD160(new byte[] { 4, 4 }).ComputeHash((Stream)null));
    }

    [Fact]
    public void GivenAHMACRIPEMD160_WhenComputeHash_VulnerabilityIsLogged3()
    {
        new HMACRIPEMD160(new byte[] { 4, 4 }).ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable();
    }

    [Fact]
    public void GivenANewHMACRIPEMD160_WhenComputeHash_VulnerabilityIsLogged()
    {
        new HMACRIPEMD160(new byte[] { 5, 5, 5 }).ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable();
    }

    [Fact]
    public void GivenANewHMACRIPEMD160_WhenComputeHash_VulnerabilityIsLogged2()
    {
        new HMACRIPEMD160(new byte[] { 5, 5, 5 }).ComputeHash(new byte[] { });
        AssertVulnerable();
    }

    [Fact]
    public void GivenANewHMACRIPEMD160_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<NullReferenceException>(() => new HMACRIPEMD160(new byte[] { 5, 5, 5 }).ComputeHash(new byte[] { 5, 5, 5 }, 10, 2));
    }
}
#endif

*/
