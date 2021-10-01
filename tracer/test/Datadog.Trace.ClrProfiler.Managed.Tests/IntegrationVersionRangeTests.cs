// <copyright file="IntegrationVersionRangeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class IntegrationVersionRangeTests
    {
        [Test]
        public void MinimumVersionTwoSetsResetsDefaultsForNonSpecifiedParts()
        {
            var range = new IntegrationVersionRange();
            range.MinimumVersion = "5.5.4";
            range.MinimumVersion = "6";
            Assert.AreEqual(expected: 6, actual: range.MinimumMajor);
            Assert.AreEqual(expected: 0, actual: range.MinimumMinor);
            Assert.AreEqual(expected: 0, actual: range.MinimumPatch);
        }

        [Test]
        public void ParsesMinimumMajor()
        {
            var range = new IntegrationVersionRange();
            range.MinimumVersion = "5";
            Assert.AreEqual(expected: 5, actual: range.MinimumMajor);
        }

        [Test]
        public void ParsesMinimumMajorAndMinor()
        {
            var range = new IntegrationVersionRange();
            range.MinimumVersion = "5.8";
            Assert.AreEqual(expected: 5, actual: range.MinimumMajor);
            Assert.AreEqual(expected: 8, actual: range.MinimumMinor);
        }

        [Test]
        public void ParsesMinimumMajorAndMinorAndPatch()
        {
            var range = new IntegrationVersionRange();
            range.MinimumVersion = "5.8.82";
            Assert.AreEqual(expected: 5, actual: range.MinimumMajor);
            Assert.AreEqual(expected: 8, actual: range.MinimumMinor);
            Assert.AreEqual(expected: 82, actual: range.MinimumPatch);
        }

        [Test]
        public void MaximumVersionTwoSetsResetsDefaultsForNonSpecifiedParts()
        {
            var range = new IntegrationVersionRange();
            range.MaximumVersion = "5.5.4";
            range.MaximumVersion = "6";
            Assert.AreEqual(expected: 6, actual: range.MaximumMajor);
            Assert.AreEqual(expected: ushort.MaxValue, actual: range.MaximumMinor);
            Assert.AreEqual(expected: ushort.MaxValue, actual: range.MaximumPatch);
        }

        [Test]
        public void ParsesMaximumMajor()
        {
            var range = new IntegrationVersionRange();
            range.MaximumVersion = "5";
            Assert.AreEqual(expected: 5, actual: range.MaximumMajor);
        }

        [Test]
        public void ParsesMaximumMajorAndMinor()
        {
            var range = new IntegrationVersionRange();
            range.MaximumVersion = "5.8";
            Assert.AreEqual(expected: 5, actual: range.MaximumMajor);
            Assert.AreEqual(expected: 8, actual: range.MaximumMinor);
        }

        [Test]
        public void ParsesMaximumMajorAndMinorAndPatch()
        {
            var range = new IntegrationVersionRange();
            range.MaximumVersion = "5.8.82";
            Assert.AreEqual(expected: 5, actual: range.MaximumMajor);
            Assert.AreEqual(expected: 8, actual: range.MaximumMinor);
            Assert.AreEqual(expected: 82, actual: range.MaximumPatch);
        }

        /// <summary>
        /// We want to be sure that any versions we specify are explicit, so we'll throw as soon as we know there is anything unclear.
        /// </summary>
        [Test]
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
