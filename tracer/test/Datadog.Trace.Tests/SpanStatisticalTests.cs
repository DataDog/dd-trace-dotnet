// <copyright file="SpanStatisticalTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    public class SpanStatisticalTests
    {
        /// <summary>
        /// The max value of the Ids we create should be a 63 bit unsigned number
        /// </summary>
        private const ulong MaxId = long.MaxValue;

        private const int NumberOfBuckets = 20;
        private const ulong NumberOfIdsToGenerate = 1_500_000;

        // Helper numbers for logging and calculating
        private const double BucketSizePercentage = 100.0 / NumberOfBuckets;
        private const ulong BucketSize = MaxId / NumberOfBuckets;

        private static readonly Dictionary<ulong, int> GeneratedIds = new();

        private readonly ITestOutputHelper _output;

        public SpanStatisticalTests(ITestOutputHelper output)
        {
            _output = output;

            if (GeneratedIds.Keys.Count > 0)
            {
                return;
            }

            _output.WriteLine("Starting key generation.");
            var stopwatch = Stopwatch.StartNew();

            // populate the dictionary for all tests
            for (ulong i = 0; i < NumberOfIdsToGenerate; i++)
            {
                var id = RandomIdGenerator.Shared.NextSpanId();

                GeneratedIds.TryGetValue(id, out var hitCount);

                hitCount++;

                GeneratedIds[id] = hitCount;
            }

            stopwatch.Stop();
            _output.WriteLine($"It took {stopwatch.ElapsedMilliseconds / 1000d} seconds to generate {NumberOfIdsToGenerate} keys.");
        }

        [Fact]
        public void GeneratedIds_Contain_High_Numbers()
        {
            const ulong rangeBound = MaxId - BucketSize;
            GeneratedIds.Keys.Should().Contain(i => i >= rangeBound);
        }

        [Fact]
        public void GeneratedIds_Contain_Low_Numbers()
        {
            GeneratedIds.Keys.Should().Contain(i => i <= BucketSize);
        }

        [Fact]
        public void GeneratedIds_Contain_Nothing_Below_Expected_Min()
        {
            GeneratedIds.Keys.Should().NotContain(i => i <= 1, "because we should never generate keys below 1.");
        }

        [Fact]
        public void GeneratedIds_Contain_Nothing_Above_Expected_Max()
        {
            GeneratedIds.Keys.Should().NotContain(i => i > MaxId, $"because we should never generate keys above {MaxId}.");
        }

        [Fact]
        public void GeneratedIds_Contain_Reasonably_Few_Duplicates()
        {
            var duplicateKeyCount = (ulong)GeneratedIds.Count(kvp => kvp.Value > 1);
            duplicateKeyCount.Should().BeLessThan(NumberOfIdsToGenerate / 1000);
        }

        [Fact]
        public void GeneratedIds_Are_Evenly_Distributed()
        {
            const ulong expectedApproximateBucketSize = NumberOfIdsToGenerate / NumberOfBuckets;
            var actualApproximateBucketSize = GeneratedIds.Count / NumberOfBuckets;
            var buckets = new List<int>();

            for (var i = 0; i < NumberOfBuckets; i++)
            {
                buckets.Add(0);
            }

            _output.WriteLine($"Requested {NumberOfIdsToGenerate} keys, received {GeneratedIds.Keys.Count} unique keys.");
            _output.WriteLine($"Expected approximately {expectedApproximateBucketSize} keys per bucket.");
            _output.WriteLine($"Receiving approximately {actualApproximateBucketSize} keys per bucket.");
            _output.WriteLine($"Organizing {NumberOfBuckets} buckets with a range size of {BucketSize} which is {BucketSizePercentage}%.");

            foreach (var key in GeneratedIds.Keys)
            {
                var percentile = (double)key / MaxId * 100;
                var bucketIndex = (int)(percentile / BucketSizePercentage);
                var numberOfHits = GeneratedIds[key];
                buckets[bucketIndex] += numberOfHits;
            }

            var emptyBuckets = new List<int>();
            long minCount = long.MaxValue;
            long maxCount = 0;

            for (var i = 0; i < NumberOfBuckets; i++)
            {
                var bucketCount = buckets[i];

                if (bucketCount == 0)
                {
                    emptyBuckets.Add(i);
                }

                if (bucketCount < minCount)
                {
                    minCount = bucketCount;
                }

                if (bucketCount > maxCount)
                {
                    maxCount = bucketCount;
                }

                var readableIndex = i + 1;
                var lowerPercent = (readableIndex - 1) * BucketSizePercentage;
                var upperPercent = (readableIndex) * BucketSizePercentage;
                _output.WriteLine($"Bucket {readableIndex} has {buckets[i]} keys between {lowerPercent}-{upperPercent}%.");
            }

            emptyBuckets.Should().BeEmpty("because there should be no empty buckets");

            // Variance is the deviation from the expected mean or average
            var maxDiff = Math.Abs(maxCount - actualApproximateBucketSize);
            var minDiff = Math.Abs(actualApproximateBucketSize - minCount);
            var biggestDiff = Math.Max(maxDiff, minDiff);

            var variance = (double)biggestDiff / actualApproximateBucketSize;
            _output.WriteLine($"The maximum variance in all buckets is {variance}.");

            const double maximumVariance = 0.05;
            variance.Should().BeLessOrEqualTo(maximumVariance, $"because the variance between buckets should be less than {maximumVariance}, but it is {variance}.");
        }
    }
}
