// <copyright file="FrameworkProviderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Datadog.RuntimeMetrics;
using Xunit;

namespace RuntimeMetrics.Tests
{
    public class FrameworkProviderTests
    {
        [Fact]
        public void CheckFrameworkProvider()
        {
            var provider = new FrameworkProvider();

            // validate garbage collection counts
            var currentGen0CollectionCount = GC.CollectionCount(0);
            var currentGen1CollectionCount = GC.CollectionCount(1);
            var currentGen2CollectionCount = GC.CollectionCount(2);
            GC.Collect(0);
            GC.Collect(1);
            GC.Collect(1);
            GC.Collect(2);
            GC.Collect(2);
            GC.Collect(2);

            var expectedGen0CollectionCount = GC.CollectionCount(0) - currentGen0CollectionCount;
            var expectedGen1CollectionCount = GC.CollectionCount(1) - currentGen1CollectionCount;
            var expectedGen2CollectionCount = GC.CollectionCount(2) - currentGen2CollectionCount;

            var metrics = provider.GetMetrics();
            var checkedMetricsCount = 0;
            var expectedMetricsCount = 3;
            for (int i = 0; i < metrics.Count; i++)
            {
                if (string.CompareOrdinal(metrics[i].Name, MetricsNames.Gen0CollectionsCount) == 0)
                {
                    Assert.True(expectedGen0CollectionCount <= TryGetValue(metrics[i].Value));
                    checkedMetricsCount++;
                }
                else
                if (string.CompareOrdinal(metrics[i].Name, MetricsNames.Gen1CollectionsCount) == 0)
                {
                    Assert.True(expectedGen1CollectionCount <= TryGetValue(metrics[i].Value));
                    checkedMetricsCount++;
                }
                else
                if (string.CompareOrdinal(metrics[i].Name, MetricsNames.Gen2CollectionsCount) == 0)
                {
                    Assert.True(expectedGen2CollectionCount <= TryGetValue(metrics[i].Value));
                    checkedMetricsCount++;
                }
            }

            Assert.Equal(expectedMetricsCount, checkedMetricsCount);
        }

        private double TryGetValue(string value)
        {
            if (double.TryParse(value, out double number))
            {
                return number;
            }

            return double.NaN;
        }
    }
}
