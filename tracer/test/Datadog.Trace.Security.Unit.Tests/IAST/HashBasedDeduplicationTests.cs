// <copyright file="HashBasedDeduplicationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Iast
{
    public class HashBasedDeduplicationTests
    {
        [Fact]
        public void GivenTheSameVulnerabilityInstance_WhenAddedToDeduplication_OnlyOneIsStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            var vulnerability = new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1));
            Assert.True(HashBasedDeduplication.Add(vulnerability));
            Assert.False(HashBasedDeduplication.Add(vulnerability));
        }

        [Fact]
        public void GivenTwoIdenticalVulnerabilities_WhenAddedToDeduplication_OnlyOneIsStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1))));
            Assert.False(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges2))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByType_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5"))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakCipher, new Location("path.cs", 23), new Evidence("MD5"))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByFile_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5"))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path2.cs", 23), new Evidence("MD5"))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByLine_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5"))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 24), new Evidence("MD5"))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByEvidence_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5"))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("DES"))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeStart_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            var ranges2 = new Range[] { new Range(2, 1, new Source(0, "sourceName", "sourceValue")) };
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges2))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeLength_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            var ranges2 = new Range[] { new Range(1, 2, new Source(0, "sourceName", "sourceValue")) };
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges2))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceOrigin_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            var ranges2 = new Range[] { new Range(1, 1, new Source(1, "sourceName", "sourceValue")) };
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges2))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceName_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName2", "sourceValue")) };
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges2))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByEvidenceSourceValue_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges2))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeSizeTwoElements_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue1")), new Range(1, 1, new Source(0, "sourceName", "sourceValue1")) };
            var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue1")), new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges2))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1))));
        }

        [Fact]
        public void GivenTwoDifferentVulnerabilitiesByEvidenceRangeSize_WhenAddedToDeduplication_BothAreStored()
        {
            HashBasedDeduplication.Clear();
            var ranges1 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue")) };
            var ranges2 = new Range[] { new Range(1, 1, new Source(0, "sourceName", "sourceValue2")), new Range(1, 1, new Source(0, "sourceName", "sourceValue2")) };
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges2))));
            Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5", ranges1))));
        }

        [Fact]
        public void GivenManyVulnerabilities_WhenAddedToDeduplication_CacheIsCleared()
        {
            HashBasedDeduplication.Clear();
            var vulnerability1 = new Vulnerability(VulnerabilityType.WeakHash, new Location("path.cs", 23), new Evidence("MD5"));
            Assert.True(HashBasedDeduplication.Add(vulnerability1));
            Assert.False(HashBasedDeduplication.Add(vulnerability1));

            for (int i = 1; i <= HashBasedDeduplication.MaximumSize; i++)
            {
                Assert.True(HashBasedDeduplication.Add(new Vulnerability(VulnerabilityType.WeakHash, new Location("pathNew.cs", i), new Evidence("MD5"))));
            }

            Assert.True(HashBasedDeduplication.Add(vulnerability1));
        }
    }
}
