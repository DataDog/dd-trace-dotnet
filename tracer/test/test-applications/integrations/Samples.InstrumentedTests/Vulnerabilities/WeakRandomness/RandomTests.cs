using System;
using FluentAssertions;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.WeakRandomness;

public class RandomTests : InstrumentationTestsBase
{
    public class TestClass : Random
    {
        public TestClass()
        {
        }
    }

    public RandomTests()
    {
    }

    [Fact]
    public void GivenATestClassDerivedFromRandom_WhenCallingConstructor_VulnerabilityIsReported()
    {
        _ = new TestClass();
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

    [Fact]
    public void GivenARandomInstance_WhenCallingNext_VulnerabilityIsReported()
    {
        var ret = new Random().Next();
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

    [Fact]
    public void GivenARandomInstance_WhenCallingConstructor_VulnerabilityIsReported()
    {
        _ = new Random(33);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

    [Fact]
    public void GivenARandomInstance_WhenCallingConstructor_VulnerabilityIsReported2()
    {
        _ = new Random();
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

    [Fact]
    public void GivenARandomInstance_WhenCallingNext_VulnerabilityIsReported2()
    {
        var ret = new Random().Next(11);
        ret.Should().BeInRange(0, 11);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

    [Fact]
    public void GivenARandomInstance_WhenCallingNext_VulnerabilityIsReported3()
    {
        var ret = new Random(55).Next(11, 556);
        ret.Should().BeInRange(11, 556);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void GivenARandomInstance_WhenCallingNextInt64_VulnerabilityIsReported()
    {
        var ret = new Random(55).NextInt64(34,44);
        ret.Should().BeInRange(34, 44);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

    [Fact]
    public void GivenARandomInstance_WhenCallingNextInt64_VulnerabilityIsReported2()
    {
        var ret = new Random().NextInt64(11);
        ret.Should().BeInRange(0, 11);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }
#endif

    [Fact]
    public void GivenARandomInstance_WhenCallingNextDouble_VulnerabilityIsReported()
    {
        var ret = new Random().NextDouble();
        ret.Should().BeInRange(0, 1);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

    [Fact]
    public void GivenARandomInstance_WhenCallingNextBytes_VulnerabilityIsReported()
    {
        byte[] buffer = new byte[4];
        new Random().NextBytes(buffer);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void GivenARandomInstance_WhenCallingNextSingle_VulnerabilityIsReported()
    {
        var ret = new Random().NextSingle();
        ret.Should().BeInRange(0, 1);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }
#endif

#if !NETFRAMEWORK
    [Fact]
    public void GivenARandomInstance_WhenCallingNextBytes_VulnerabilityIsReported2()
    {
        var buffer = new Span<byte> (new byte[100]);
        new Random().NextBytes(buffer);
        AssertVulnerable("WEAK_RANDOMNESS", evidenceTainted: false);
    }
#endif
}

