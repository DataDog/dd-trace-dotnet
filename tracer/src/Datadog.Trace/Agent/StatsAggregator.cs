// <copyright file="StatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Agent
{
    internal class StatsAggregator : IDisposable
    {
        private const int BufferCount = 2;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<StatsAggregator>();

        private readonly StatsBuffer[] _buffers;

        private readonly IApi _api;

        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly TimeSpan _duration;

        private int _currentBuffer;

        public StatsAggregator(IApi api, ImmutableTracerSettings settings)
             : this(api, settings, TimeSpan.FromSeconds(10))
        {
        }

        internal StatsAggregator(IApi api, ImmutableTracerSettings settings, TimeSpan duration)
        {
            _api = api;
            _cancellationTokenSource = new CancellationTokenSource();
            _duration = duration;
            _buffers = new StatsBuffer[BufferCount];

            var header = new ClientStatsPayload
            {
                Environment = settings.Environment,
                ServiceName = settings.ServiceName,
                Version = settings.ServiceVersion,
                HostName = HostMetadata.Instance.Hostname
            };

            for (int i = 0; i < _buffers.Length; i++)
            {
                _buffers[i] = new(header);
            }

            if (settings.TracerStatsEnabled)
            {
                _ = Task.Run(() => Flush(_cancellationTokenSource.Token), _cancellationTokenSource.Token)
                    .ContinueWith(t => Log.Error(t.Exception, "Error in StatsAggregator"), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        /// <summary>
        /// Gets the current buffer.
        /// StatsBuffer is not thread-safe, this property is not intended to be used outside of the class,
        /// except for tests.
        /// </summary>
        internal StatsBuffer CurrentBuffer => _buffers[_currentBuffer];

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
        }

        public void Add(params Span[] spans)
        {
            AddRange(spans, 0, spans.Length);
        }

        public void AddRange(Span[] spans, int offset, int count)
        {
            // Contention around this lock is expected to be very small:
            // AddRange is called from the serialization thread, and concurrent serialization
            // of traces is a rare corner-case (happening only during shutdown).
            // The Flush thread only acquires the lock long enough to swap the metrics buffer.
            lock (_buffers)
            {
                for (int i = 0; i < count; i++)
                {
                    Add(spans[offset + i]);
                }
            }
        }

        internal async Task Flush(CancellationToken cancellationToken)
        {
            // Use a do/while loop to still flush once if the token is already cancelled (this makes testing easier)
            do
            {
                var buffer = CurrentBuffer;

                lock (_buffers)
                {
                    _currentBuffer = (_currentBuffer + 1) % BufferCount;
                }

                if (buffer.Buckets.Count > 0)
                {
                    // Push the metrics
                    await _api.SendStatsAsync(buffer, _duration.ToNanoseconds()).ConfigureAwait(false);

                    buffer.Reset();
                }

                await Task.Delay(_duration, cancellationToken).ConfigureAwait(false);
            }
            while (!cancellationToken.IsCancellationRequested);
        }

        /// <summary>
        /// Converts a nanosec timestamp into a float nanosecond timestamp truncated to a fixed precision
        /// </summary>
        /// <param name="ns">Timestamp to convert</param>
        /// <returns>Timestamp with truncated precision</returns>
        private static double ConvertTimestamp(long ns)
        {
            // 10 bits precision (any value will be +/- 1/1024)
            const long roundMask = 1 << 10;

            int shift = 0;

            while (ns > roundMask)
            {
                ns >>= 1;
                shift++;
            }

            return ns << shift;
        }

        private static StatsAggregationKey BuildKey(Span span)
        {
            var rawHttpStatusCode = span.GetTag(Tags.HttpStatusCode);

            if (rawHttpStatusCode == null || !int.TryParse(rawHttpStatusCode, out var httpStatusCode))
            {
                httpStatusCode = 0;
            }

            return new StatsAggregationKey(
                span.ResourceName,
                span.ServiceName,
                span.OperationName,
                span.Type,
                httpStatusCode);
        }

        private void Add(Span span)
        {
            if (!span.IsTopLevel && span.GetMetric(Tags.Measured) != 1.0)
            {
                return;
            }

            var key = BuildKey(span);

            var buffer = CurrentBuffer;

            if (!buffer.Buckets.TryGetValue(key, out var bucket))
            {
                bucket = new StatsBucket(key);
                buffer.Buckets.Add(key, bucket);
            }

            bucket.Hits++;

            if (span.IsTopLevel)
            {
                bucket.TopLevelHits++;
            }

            var duration = span.Duration.ToNanoseconds();

            bucket.Duration += duration;

            if (span.Error)
            {
                bucket.Errors++;
                bucket.ErrorSummary.Add(ConvertTimestamp(duration));
            }
            else
            {
                bucket.OkSummary.Add(ConvertTimestamp(duration));
            }
        }
    }
}
