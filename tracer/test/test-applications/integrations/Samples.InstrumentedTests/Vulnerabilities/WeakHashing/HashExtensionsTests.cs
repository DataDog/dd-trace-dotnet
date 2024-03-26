// <copyright file="HashExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakHashing;

public class HashExtensionsTests : InstrumentationTestsBase
{
    [Fact]
    public void GivenAHashExtensionsSha256_WhenCreating_VulnerabilityIsNotLogged()
    {
        HashExtensions.Sha256("test");
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAHashExtensionsSha256_WhenCreating_VulnerabilityIsNotLogged2()
    {
        HashExtensions.Sha256(new byte[] { 5, 5, 5 });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAHashExtensionsSha512_WhenCreating_VulnerabilityIsNotLogged()
    {
        HashExtensions.Sha512("test");
        AssertNotVulnerable();
    }
}
