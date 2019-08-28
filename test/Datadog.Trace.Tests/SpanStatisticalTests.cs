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
        private static ulong _bucketSize = _maxId / 20;
        private static ulong _numberOfIdsToGenerate = 20_000_000;

        private readonly ITestOutputHelper _output;

        public SpanStatisticalTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GeneratedIds_Contain_High_Numbers()
        {
            BlastOff();
            var rangeBound = _maxId - _bucketSize;
            var keysWithinRange = _generatedIds.Keys.Where(i => i >= rangeBound).ToList();
            _output.WriteLine($"Found {keysWithinRange.Count()} above {rangeBound}.");
            Assert.True(keysWithinRange.Count() > 0);
        }

        [Fact]
        public void GeneratedIds_Contain_Low_Numbers()
        {
            BlastOff();
            var rangeBound = _bucketSize;
            var keysWithinRange = _generatedIds.Keys.Where(i => i <= rangeBound).ToList();
            _output.WriteLine($"Found {keysWithinRange.Count()} below {rangeBound}.");
            Assert.True(keysWithinRange.Count() > 0);
        }

        [Fact]
        public void GeneratedIds_Contain_Reasonably_Few_Duplicates()
        {
            BlastOff();
            var duplicateKeys = _generatedIds.Where(kvp => kvp.Value > 1).ToList();
            var acceptablePercentageOfDuplicates = 0.01m;

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
            BlastOff();
            var currentBucket = _bucketSize;
            var orderedKeys = _generatedIds.Keys.OrderBy(key => key);
            var buckets = new Dictionary<ulong, ulong>();

            buckets.Add(currentBucket, 0);

            foreach (var key in orderedKeys)
            {
                if (key <= currentBucket)
                {
                    // Add the number of keys to the bucket
                    buckets[currentBucket] += _generatedIds[key];
                }
                else
                {
                    // Time to start new buckets
                    while (key > currentBucket)
                    {
                        currentBucket += _bucketSize;
                        buckets.Add(currentBucket, 0);
                    }

                    buckets[currentBucket] += _generatedIds[key];
                }
            }

            var bucketsWithNoKeys = new List<ulong>();
            ulong minCount = ulong.MaxValue;
            ulong maxCount = 0;
            var orderedBucketKeys = buckets.Keys.OrderBy(key => key);

            foreach (var bucketKey in orderedBucketKeys)
            {
                var bucketCount = buckets[bucketKey];

                if (bucketCount == 0)
                {
                    bucketsWithNoKeys.Add(bucketKey);
                    continue;
                }

                if (bucketCount < minCount)
                {
                    minCount = bucketCount;
                }

                if (bucketCount > maxCount)
                {
                    maxCount = bucketCount;
                }
            }

            Assert.True(bucketsWithNoKeys.Count() == 0, "There should be no buckets which have no keys.");

            var variance = (decimal)(maxCount - minCount) / (decimal)maxCount;
            var maximumVariance = 0.01m;
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
