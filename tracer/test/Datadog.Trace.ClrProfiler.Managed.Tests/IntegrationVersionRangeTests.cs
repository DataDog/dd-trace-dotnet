// <copyright file="IntegrationVersionRangeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class IntegrationVersionRangeTests
    {
        [Fact]
        public void MinimumVersionTwoSetsResetsDefaultsForNonSpecifiedParts()
        {
            var range = new IntegrationVersionRange();
            range.MinimumVersion = "5.5.4";
            range.MinimumVersion = "6";
            Assert.Equal(expected: 6, actual: range.MinimumMajor);
            Assert.Equal(expected: 0, actual: range.MinimumMinor);
            Assert.Equal(expected: 0, actual: range.MinimumPatch);
        }

        [Fact]
        public void ParsesMinimumMajor()
        {
            var range = new IntegrationVersionRange();
            range.MinimumVersion = "5";
            Assert.Equal(expected: 5, actual: range.MinimumMajor);
        }

        [Fact]
        public void ParsesMinimumMajorAndMinor()
        {
            var range = new IntegrationVersionRange();
            range.MinimumVersion = "5.8";
            Assert.Equal(expected: 5, actual: range.MinimumMajor);
            Assert.Equal(expected: 8, actual: range.MinimumMinor);
        }

        [Fact]
        public void ParsesMinimumMajorAndMinorAndPatch()
        {
            var range = new IntegrationVersionRange();
            range.MinimumVersion = "5.8.82";
            Assert.Equal(expected: 5, actual: range.MinimumMajor);
            Assert.Equal(expected: 8, actual: range.MinimumMinor);
            Assert.Equal(expected: 82, actual: range.MinimumPatch);
        }

        [Fact]
        public void MaximumVersionTwoSetsResetsDefaultsForNonSpecifiedParts()
        {
            var range = new IntegrationVersionRange();
            range.MaximumVersion = "5.5.4";
            range.MaximumVersion = "6";
            Assert.Equal(expected: 6, actual: range.MaximumMajor);
            Assert.Equal(expected: ushort.MaxValue, actual: range.MaximumMinor);
            Assert.Equal(expected: ushort.MaxValue, actual: range.MaximumPatch);
        }

        [Fact]
        public void ParsesMaximumMajor()
        {
            var range = new IntegrationVersionRange();
            range.MaximumVersion = "5";
            Assert.Equal(expected: 5, actual: range.MaximumMajor);
        }

        [Fact]
        public void ParsesMaximumMajorAndMinor()
        {
            var range = new IntegrationVersionRange();
            range.MaximumVersion = "5.8";
            Assert.Equal(expected: 5, actual: range.MaximumMajor);
            Assert.Equal(expected: 8, actual: range.MaximumMinor);
        }

        [Fact]
        public void ParsesMaximumMajorAndMinorAndPatch()
        {
            var range = new IntegrationVersionRange();
            range.MaximumVersion = "5.8.82";
            Assert.Equal(expected: 5, actual: range.MaximumMajor);
            Assert.Equal(expected: 8, actual: range.MaximumMinor);
            Assert.Equal(expected: 82, actual: range.MaximumPatch);
        }

        /// <summary>
        /// We want to be sure that any versions we specify are explicit, so we'll throw as soon as we know there is anything unclear.
        /// </summary>
        [Fact]
        public void ThrowsExceptionForNonNumbers()
        {
            Exception exMin = null;
            Exception exMax = null;
            var range = new IntegrationVersionRange();

            try
            {
                range.MinimumVersion = "5.GARBAGE.82";
            }
            catch (Exception e)
            {
                exMin = e;
            }

            try
            {
                range.MaximumVersion = "5.35.MoreGarbage";
            }
            catch (Exception e)
            {
                exMax = e;
            }

            Assert.NotNull(exMin);
            Assert.NotNull(exMax);
        }
    }
}
