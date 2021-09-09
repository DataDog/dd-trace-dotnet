// <copyright file="SpanStatisticalTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests
{
    public class SpanStatisticalTests
    {
        private static readonly object _populationLock = new object();
        private static readonly ConcurrentDictionary<ulong, ulong> _generatedIds = new ConcurrentDictionary<ulong, ulong>();

        /// <summary>
        /// The max value of the Ids we create should be a 63 bit unsigned number
        /// </summary>
        private static ulong _maxId = ulong.MaxValue / 2;
        private static int _numberOfBuckets = 20;
        private static ulong _numberOfIdsToGenerate = 1_500_000;

        // Helper numbers for logging and calculating
        private static decimal _bucketSizePercentage = 100 / _numberOfBuckets;
        private static ulong _bucketSize = _maxId / (ulong)_numberOfBuckets;

        private readonly ITestOutputHelper _output;

        public SpanStatisticalTests(ITestOutputHelper output)
        {
            _output = output;
            BlastOff();
        }

        [Fact]
        public void GeneratedIds_Contain_High_Numbers()
        {
            var rangeBound = _maxId - _bucketSize;
            var keysWithinRange = _generatedIds.Keys.Where(i => i >= rangeBound).ToList();
            _output.WriteLine($"Found {keysWithinRange.Count()} above {rangeBound}, the top {_bucketSizePercentage}% of values.");
            Assert.True(keysWithinRange.Count() > 0);
        }

        [Fact]
        public void GeneratedIds_Contain_Low_Numbers()
        {
            var rangeBound = _bucketSize;
            var keysWithinRange = _generatedIds.Keys.Where(i => i <= rangeBound).ToList();
            _output.WriteLine($"Found {keysWithinRange.Count()} below {rangeBound}, the bottom {_bucketSizePercentage}% of values.");
            Assert.True(keysWithinRange.Count() > 0);
        }

        [Fact]
        public void GeneratedIds_Contain_Nothing_Above_Expected_Max()
        {
            var keysOutOfRange = _generatedIds.Keys.Any(i => i > _maxId);
            Assert.False(keysOutOfRange, $"We should never generate keys above {_maxId}.");
        }

        [Fact]
        public void GeneratedIds_Contain_Reasonably_Few_Duplicates()
        {
            var duplicateKeys = _generatedIds.Where(kvp => kvp.Value > 1).ToList();
            var acceptablePercentageOfDuplicates = 0.001m;

            ulong duplicateKeyCount = 0;
            foreach (var kvp in duplicateKeys)
            {
                duplicateKeyCount += kvp.Value;
            }

            var percentageOfDuplicates = (decimal)duplicateKeyCount / (decimal)_numberOfIdsToGenerate;
            _output.WriteLine($"Found {duplicateKeyCount} duplicate keys.");
            Assert.True(percentageOfDuplicates <= acceptablePercentageOfDuplicates);
        }

        [Fact]
        public void GeneratedIds_Are_Evenly_Distributed()
        {
            var expectedApproximateBucketSize = _numberOfIdsToGenerate / (ulong)_numberOfBuckets;
            var actualApproximateBucketSize = (ulong)_generatedIds.Keys.Count() / (ulong)_numberOfBuckets;
            var buckets = new List<ulong>();
            for (var i = 0; i < _numberOfBuckets; i++)
            {
                buckets.Add(0);
            }

            _output.WriteLine($"Requested {_numberOfIdsToGenerate} keys, received {_generatedIds.Keys.Count()} unique keys.");
            _output.WriteLine($"Expected approximately {expectedApproximateBucketSize} keys per bucket.");
            _output.WriteLine($"Receiving approximately {actualApproximateBucketSize} keys per bucket.");
            _output.WriteLine($"Organizing {_numberOfBuckets} buckets with a range size of {_bucketSize} which is {_bucketSizePercentage}%.");

            foreach (var key in _generatedIds.Keys)
            {
                var percentile = ((decimal)key / _maxId) * 100m;
                var bucketIndex = (int)(percentile / _bucketSizePercentage);
                var numberOfHits = _generatedIds[key];
                buckets[bucketIndex] += numberOfHits;
            }

            var bucketsWithNoKeys = new List<int>();
            ulong minCount = ulong.MaxValue;
            ulong maxCount = 0;

            for (var i = 0; i < _numberOfBuckets; i++)
            {
                var bucketCount = buckets[i];
                if (bucketCount == 0)
                {
                    bucketsWithNoKeys.Add(i);
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
                var lowerPercent = (readableIndex - 1) * _bucketSizePercentage;
                var upperPercent = (readableIndex) * _bucketSizePercentage;
                _output.WriteLine($"Bucket {readableIndex} has {buckets[i]} keys between {lowerPercent}-{upperPercent}%.");
            }

            Assert.True(bucketsWithNoKeys.Count() == 0, "There should be no buckets which have no keys.");

            // Variance is the deviation from the expected mean or average
            var maxDiff = Math.Abs((decimal)(maxCount - actualApproximateBucketSize));
            var minDiff = Math.Abs((decimal)(actualApproximateBucketSize - minCount));
            var biggestDiff = new[] { maxDiff, minDiff }.Max();
            var variance = biggestDiff / actualApproximateBucketSize;

            var maximumVariance = 0.05m;
            _output.WriteLine($"The maximum variance in all buckets is {variance}.");
            Assert.True(maximumVariance >= variance, $"The variance between buckets should be less than {maximumVariance}, but it is {variance}.");
        }

        private void BlastOff()
        {
            lock (_populationLock)
            {
                if (_generatedIds.Keys.Count > 0)
                {
                    return;
                }

                _output.WriteLine($"Starting key generation.");
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                // populate the dictionary for all tests
                Parallel.For(0L, (long)_numberOfIdsToGenerate, i =>
                {
                    var id = GenerateId();
                    _generatedIds.AddOrUpdate(
                        key: id,
                        addValue: 1,
                        updateValueFactory: (key, oldValue) => oldValue++);
                });
                stopwatch.Stop();
                _output.WriteLine($"It took {stopwatch.ElapsedMilliseconds / 1000d} seconds to generate {_numberOfIdsToGenerate} keys.");
            }
        }

        private ulong GenerateId()
        {
            return new SpanContext(null, null, string.Empty).SpanId;
        }
    }
}
