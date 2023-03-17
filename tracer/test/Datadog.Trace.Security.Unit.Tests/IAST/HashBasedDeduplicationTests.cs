// <copyright file="HashBasedDeduplicationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast;

public class HashBasedDeduplicationTests
{
    [Fact]
    public void GivenTheSameVulnerabilityInstance_WhenAddedToDeduplication_OnlyOneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var vulnerability = new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1));
        Assert.True(instance.Add(vulnerability));
        Assert.False(instance.Add(vulnerability));
    }

    [Fact]
    public void GivenTwoIdenticalVulnerabilities_WhenAddedToDeduplication_OnlyOneIsStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1))));
        Assert.False(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges2))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByType_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5"))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakCipher, new Location("path.cs", 23, 0), new Evidence("MD5"))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByFile_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5"))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path2.cs", 23, 0), new Evidence("MD5"))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByLine_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5"))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 24, 0), new Evidence("MD5"))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidence_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5"))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("DES"))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeStart_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(2, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges2))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeLength_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 2, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges2))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceOrigin_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(1, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges2))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceName_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName2", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges2))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceValue_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges2))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeSizeTwoElements_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue1")), new Range(1, 1, new Source(0, "sourceName", "sourceValue1")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue1")), new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges2))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeSize_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue2")), new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges2))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenTwoDifferentVulnerabilitiesBySpanId_WhenAddedToDeduplication_BothAreStored()
    {
        var instance = new HashBasedDeduplication();
        var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 1), new Evidence("MD5", ranges1))));
        Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 33), new Evidence("MD5", ranges1))));
    }

    [Fact]
    public void GivenManyVulnerabilities_WhenAddedToDeduplication_CacheIsCleared()
    {
        var instance = new HashBasedDeduplication();
        var vulnerability1 = new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("path.cs", 23, 0), new Evidence("MD5"));
        Assert.True(instance.Add(vulnerability1));
        Assert.False(instance.Add(vulnerability1));

        for (int i = 1; i <= HashBasedDeduplication.MaximumSize; i++)
        {
            Assert.True(instance.Add(new Vulnerability(VulnerabilityTypeName.WeakHash, new Location("pathNew.cs", i, 0), new Evidence("MD5"))));
        }

        Assert.True(instance.Add(vulnerability1));
    }
}
