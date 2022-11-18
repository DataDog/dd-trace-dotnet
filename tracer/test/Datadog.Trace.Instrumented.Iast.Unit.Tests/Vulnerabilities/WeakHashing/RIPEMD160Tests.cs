// <copyright file="RIPEMD160Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

/*

#if !NET5_0_OR_GREATER
using System;
using System.IO;
using Moq;
using Xunit;

namespace Datadog.Trace.Instrumented.Iast.Unit.Tests.Vulnerabilities.WeakHashing;

public class RIPEMD160Tests : InstrumentationTestsBase
{
#if !NETFRAMEWORK
    [Fact]
    public void GivenANewRIPEMD160_WhenComputeHash_VulnerabilityIsLogged()
    {
        new RIPEMD160().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable();
    }

    [Fact]
    public void GivenANewRIPEMD160_WhenComputeHash_VulnerabilityIsLogged2()
    {
        new RIPEMD160().ComputeHash(new byte[] { });
        AssertVulnerable();
    }

    [Fact]

    public void GivenANewRIPEMD160_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => new RIPEMD160().ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenANewRIPEMD160_WhenComputeHash_ExceptionIsThrown2()
    {
        Assert.Throws<NullReferenceException>(() => new RIPEMD160().ComputeHash(((Stream)null)));
    }

    [Fact]
    public void GivenANewRIPEMD160_WhenComputeHash_VulnerabilityIsLogged3()
    {
        new RIPEMD160().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable();
    }
#endif
#if NETFRAMEWORK
    [Fact]
    public void GivenARIPEMD160_WhenCreating_VulnerabilityIsNotLogged()
    {
        RIPEMD160.Create();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenARIPEMD160_WhenComputeHash_VulnerabilityIsLogged()
    {
        RIPEMD160.Create().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable();
    }

    [Fact]
    public void GivenARIPEMD160_WhenComputeHash_VulnerabilityIsLogged2()
    {
        RIPEMD160.Create().ComputeHash(new byte[] { });
        AssertVulnerable();
    }

    [Fact]
    public void GivenARIPEMD160_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => RIPEMD160.Create().ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenARIPEMD160_WhenComputeHash_ExceptionIsThrown2()
    {
        Assert.Throws<NullReferenceException>(() => RIPEMD160.Create().ComputeHash((Stream)null));
    }

    [Fact]
    public void GivenARIPEMD160_WhenComputeHash_VulnerabilityIsLogged3()
    {
        RIPEMD160.Create().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable();
    }

    [Fact]
    public void GivenARIPEMD160Managed_WhenComputeHash_VulnerabilityIsLogged()
    {
        RIPEMD160Managed.Create().ComputeHash(new Mock<Stream>().Object);
        AssertVulnerable();
    }

    [Fact]
    public void GivenARIPEMD160Managed_WhenComputeHash_VulnerabilityIsLogged2()
    {
        RIPEMD160Managed.Create().ComputeHash(new byte[] { });
        AssertVulnerable();
    }

    [Fact]
    public void GivenARIPEMD160Managed_WhenComputeHash_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => RIPEMD160Managed.Create().ComputeHash(new byte[] { }, 10, 10));
    }

    [Fact]
    public void GivenARIPEMD160Managed_WhenComputeHash_ExceptionIsThrown2()
    {
        Assert.Throws<NullReferenceException>(() => RIPEMD160Managed.Create().ComputeHash((Stream)null));
    }

    [Fact]
    public void GivenARIPEMD160Managed_WhenComputeHash_VulnerabilityIsLogged3()
    {
        RIPEMD160Managed.Create().ComputeHash(new byte[] { 5, 5, 5 }, 0, 2);
        AssertVulnerable();
    }
#endif
}
#endif

*/
