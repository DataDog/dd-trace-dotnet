// <copyright file="HashBasedDeduplicationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast;

public class HashBasedDeduplicationTests
{
    [Fact]
    public void GivenTheSameVulnerabilityInstance_WhenAddedToDeduplication_OnlyOneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var vulnerability = new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1));
        Assert.True(instance.Add(vulnerability));
        Assert.False(instance.Add(vulnerability));
    }

    [Fact]
    public void GivenTwoIdenticalVulnerabilities_WhenAddedToDeduplication_OnlyOneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges2))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByType_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5"))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakCipher, new Location("path.cs", null, 23, 0, null), new Evidence("MD5"))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByFile_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5"))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path2.cs", null, 23, 0, null), new Evidence("MD5"))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByLine_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5"))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 24, 0, null), new Evidence("MD5"))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidence_WhenAddedToDeduplication_OnlyOneIsStored()
    {
        var instance = new HashBasedDeduplication();
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5"))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("DES"))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeStart_WhenAddedToDeduplication_OnlyOneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(2, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges2))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeLength_WhenAddedToDeduplication_OneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 2, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges2))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceOrigin_WhenAddedToDeduplication_OneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(1, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges2))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceName_WhenAddedToDeduplication_OneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName2", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges2))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceValue_WhenAddedToDeduplication_OneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges2))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeSizeTwoElements_WhenAddedToDeduplication_OneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue1")), new Range(1, 1, new Source(0, "sourceName", "sourceValue1")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue1")), new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges2))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeSize_WhenAddedToDeduplication_OnlyOneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue2")), new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges2))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesBySpanId_WhenAddedToDeduplication_OnlyOneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 1, null), new Evidence("MD5", ranges1))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 33, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByMethod_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location(null, "method1", null, 33, null), new Evidence("MD5", ranges1))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location(null, "method2", null, 33, null), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByMethodType_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location(null, "method1", null, 33, "type1"), new Evidence("MD5", ranges1))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location(null, "method1", null, 33, "type2"), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesEqualMethodType_WhenAddedToDeduplication_OneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location(null, "method1", null, 33, "type1"), new Evidence("MD5", ranges1))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location(null, "method1", null, 33, "type1"), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenManyVulnerabilities_WhenAddedToDeduplication_CacheIsCleared()
    {
        var instance = new HashBasedDeduplication();
        var vulnerability1 = new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", null, 23, 0, null), new Evidence("MD5"));
        Assert.True(instance.Add(vulnerability1));
        Assert.False(instance.Add(vulnerability1));

        for (int i = 1; i <= HashBasedDeduplication.MaximumSize; i++)
        {
            Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("pathNew.cs", null, i, 0, null), new Evidence("MD5"))));
        }

        Assert.True(instance.Add(vulnerability1));
    }

    [Theory]
    [InlineData(10, false, 55, true)]
    [InlineData(60, true, 55, false)]
    [InlineData(6, false, 5, false)]
    [InlineData(61, true, 65, true)]
    public void GivenHashBasedDeduplication_WhenTestDeduplicationTimeout_ResultIsOk(int minutesAfter1, bool expectedResult1, int minutesAfter2, bool expectedResult2)
    {
        var date = new System.DateTime(2001, 1, 1, 1, 0, 0).ToUniversalTime();
        var instance = new HashBasedDeduplication(date);
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.NoSameSiteCookie, null, new Evidence("value")), date));
        date = date.AddMinutes(minutesAfter1);
        var result = instance.Add(new Vulnerability(VulnerabilityTypeName.NoSameSiteCookie, null, new Evidence("value")), date);
        result.Should().Be(expectedResult1);
        date = date.AddMinutes(minutesAfter2);
        result = instance.Add(new Vulnerability(VulnerabilityTypeName.NoSameSiteCookie, null, new Evidence("value")), date);
        result.Should().Be(expectedResult2);
    }
}
