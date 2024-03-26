// <copyright file="WeakCipherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Security.Cryptography;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakCipher;

#pragma warning disable SYSLIB0021 // Type or member is obsolete
#pragma warning disable SYSLIB0022 // Type or member is obsolete
#pragma warning disable SYSLIB0045 // Type or member is obsolete
public class WeakCipherTests : InstrumentationTestsBase
{
    [Fact]
    public void GivenADes_WhenCreating_VulnerabilityIsLogged()
    {
        DES.Create();
        AssertVulnerable(evidenceTainted: false);
    }

    [Fact]
    public void GivenADESCryptoServiceProvider_WhenCreating_VulnerabilityIsLogged()
    {
        new DESCryptoServiceProvider();
        AssertVulnerable(evidenceTainted: false);
    }

    [Fact]
    public void GivenARC2_WhenCreating_VulnerabilityIsLogged()
    {
        RC2.Create();
        AssertVulnerable(evidenceTainted: false);
    }

    [Fact]
    public void GivenARC2CryptoServiceProvider_WhenCreating_VulnerabilityIsLogged()
    {
        new RC2CryptoServiceProvider();
        AssertVulnerable(evidenceTainted: false);
    }

    [Fact]
    public void GivenATripleDES_WhenCreating_VulnerabilityIsLogged()
    {
        TripleDES.Create();
        AssertVulnerable(evidenceTainted: false);
    }

    [Fact]
    public void GivenATripleDESCryptoServiceProvider_WhenCreating_VulnerabilityIsLogged()
    {
        new TripleDESCryptoServiceProvider();
        AssertVulnerable(evidenceTainted: false);
    }

    [Fact]
    public void GivenARijndael_WhenCreating_VulnerabilityIsNotLogged()
    {
        Rijndael.Create();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenRijndaelManaged_WhenCreating_VulnerabilityIsNotLogged()
    {
        new RijndaelManaged();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAes_WhenCreating_VulnerabilityIsNotLogged()
    {
        Aes.Create();
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAesCryptoServiceProvider_WhenCreating_VulnerabilityIsNotLogged()
    {
        new AesCryptoServiceProvider();
        AssertNotVulnerable();
    }
}
