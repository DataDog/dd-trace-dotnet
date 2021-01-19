using System;
using System.Collections.Concurrent;

using Datadog.Trace.Util;

namespace Datadog.Trace.Logging
{
    internal class LogRateLimiter : ILogRateLimiter
    {
#if NET45
        private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
#endif

        private readonly int _secondsBetweenLogs;
        private readonly ConcurrentDictionary<int, LogRateBucketInfo> _buckets
            = new ConcurrentDictionary<int, LogRateBucketInfo>();

        public LogRateLimiter(int secondsBetweenLogs)
        {
            if (secondsBetweenLogs < 1)
            {
                throw new ArgumentException("Must have positive number of seconds between logs", nameof(secondsBetweenLogs));
            }

            _secondsBetweenLogs = secondsBetweenLogs;
        }

        /// <inheritdoc/>
        public bool ShouldLog(string filePath, int lineNumber, out uint skipCount)
        {
            // RFC says we should take log context, level, filename and lineNumber into account
            // but we don't currently set the log context name in IDatadogLogger. FilePath and
            // lineNumber should generally sufficient to uniquely identify the log given our API anyway
            var key = HashCode.Combine(filePath, lineNumber);

#if NET45
            TimeSpan diff = Clock.UtcNow - _unixEpoch;
            var timestamp = diff.TotalSeconds;
#else
            var timestamp = ((DateTimeOffset)Clock.UtcNow).ToUnixTimeSeconds();
#endif

            var currentTimeBucket = (int)(timestamp / _secondsBetweenLogs);
            System.Diagnostics.Debug.Assert(currentTimeBucket > 0, $"Time bucket should be greater than 0");

            var newLogInfo = _buckets.AddOrUpdate(
                key,
                new LogRateBucketInfo(currentTimeBucket, skipCount: 0, 0),
                (key, prev) => GetUpdatedLimitInfo(prev, currentTimeBucket));

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

        public struct LogRateBucketInfo
        {
            public int TimeBucket;
            public uint SkipCount;
            public uint PreviousSkipCount;

            public LogRateBucketInfo(int timeBucket, uint skipCount, uint previousSkipCount)
            {
                TimeBucket = timeBucket;
                SkipCount = skipCount;
                PreviousSkipCount = previousSkipCount;
            }
        }
    }
}
