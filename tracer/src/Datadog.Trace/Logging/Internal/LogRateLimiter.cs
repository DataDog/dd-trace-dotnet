// <copyright file="LogRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;

using Datadog.Trace.Util;

namespace Datadog.Trace.Logging
{
    internal class LogRateLimiter : ILogRateLimiter
    {
        private readonly int _secondsBetweenLogs;
        private readonly ConcurrentDictionary<LogRateBucketKey, LogRateBucketInfo> _buckets = new();

        public LogRateLimiter(int secondsBetweenLogs)
        {
            if (secondsBetweenLogs < 1)
            {
                ThrowHelper.ThrowArgumentException("Must have positive number of seconds between logs", nameof(secondsBetweenLogs));
            }

            _secondsBetweenLogs = secondsBetweenLogs;
        }

        /// <inheritdoc/>
        public bool ShouldLog(string filePath, int lineNumber, out uint skipCount)
        {
            if (filePath == string.Empty || lineNumber == 0)
            {
                // Shouldn't happen, but playing it safe incase there's a problem with the attributes
                skipCount = 0;
                return true;
            }

            // RFC says we should take log context, level, filename and lineNumber into account
            // but we don't currently set the log context name in IDatadogLogger. FilePath and
            // lineNumber should generally sufficient to uniquely identify the log given our API anyway
            var key = new LogRateBucketKey(filePath, lineNumber);

            var timestamp = ((DateTimeOffset)Clock.UtcNow).ToUnixTimeSeconds();

            var currentTimeBucket = (int)(timestamp / _secondsBetweenLogs);
            System.Diagnostics.Debug.Assert(currentTimeBucket > 0, $"Time bucket should be greater than 0");

            var newLogInfo = _buckets.AddOrUpdate(
                key,
                new LogRateBucketInfo(currentTimeBucket, skipCount: 0, 0),
                (_, prev) => GetUpdatedLimitInfo(prev, currentTimeBucket));

            skipCount = newLogInfo.PreviousSkipCount;
            return newLogInfo.SkipCount == 0;
        }

        private static LogRateBucketInfo GetUpdatedLimitInfo(LogRateBucketInfo previous, int currentTimeBucket)
        {
            if (previous.TimeBucket == currentTimeBucket)
            {
                return new LogRateBucketInfo(currentTimeBucket, previous.SkipCount + 1, previous.PreviousSkipCount);
            }

            return new LogRateBucketInfo(currentTimeBucket, skipCount: 0, previous.SkipCount);
        }

        public readonly struct LogRateBucketInfo
        {
            public readonly int TimeBucket;
            public readonly uint SkipCount;
            public readonly uint PreviousSkipCount;

            public LogRateBucketInfo(int timeBucket, uint skipCount, uint previousSkipCount)
            {
                TimeBucket = timeBucket;
                SkipCount = skipCount;
                PreviousSkipCount = previousSkipCount;
            }
        }

        public readonly struct LogRateBucketKey
        {
            public readonly string FilePath;
            public readonly int LineNo;

            public LogRateBucketKey(string filePath, int lineNo)
            {
                FilePath = filePath;
                LineNo = lineNo;
            }

            public override bool Equals(object? obj)
            {
                return obj is LogRateBucketKey key &&
                       FilePath == key.FilePath &&
                       LineNo == key.LineNo;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(FilePath, LineNo);
            }
        }
    }
}
