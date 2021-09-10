// <copyright file="BatchedConnectionStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Based on https://github.com/serilog/serilog-sinks-periodicbatching/blob/66a74768196758200bff67077167cde3a7e346d5/src/Serilog.Sinks.PeriodicBatching/Sinks/PeriodicBatching/BatchedConnectionStatus.cs
// Copyright 2013-2020 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

namespace Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching
{
    /// <summary>
    /// Manages reconnection period and transient fault response for <see cref="BatchingSink"/>.
    /// During normal operation an object of this type will simply echo the configured batch transmission
    /// period. When availability fluctuates, the class tracks the number of failed attempts, each time
    /// increasing the interval before reconnection is attempted (up to a set maximum) and at predefined
    /// points indicating that either the current batch, or entire waiting queue, should be dropped. This
    /// Serves two purposes - first, a loaded receiver may need a temporary reduction in traffic while coming
    /// back online. Second, the sender needs to account for both bad batches (the first fault response) and
    /// also overproduction (the second, queue-dropping response). In combination these should provide a
    /// reasonable delivery effort but ultimately protect the sender from memory exhaustion.
    /// </summary>
    /// <remarks>
    /// Currently used only by <see cref="BatchingSink"/>, but may
    /// provide the basis for a "smart" exponential backoff timer. There are other factors to consider
    /// including the desire to send batches "when full" rather than continuing to buffer, and so-on.
    /// </remarks>
    internal class BatchedConnectionStatus
    {
        private const int FailuresBeforeDroppingBatch = 8;
        private const int FailuresBeforeDroppingQueue = 10;

        private static readonly TimeSpan MinimumBackoffPeriod = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaximumBackoffInterval = TimeSpan.FromMinutes(10);

        private readonly TimeSpan _period;

        private int _failuresSinceSuccessfulBatch;

        public BatchedConnectionStatus(TimeSpan period)
        {
            if (period < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(period), "The batching period must be a positive timespan");
            }

            _period = period;
        }

        public TimeSpan NextInterval
        {
            get
            {
                // Available, and first failure, just try the batch interval
                if (_failuresSinceSuccessfulBatch <= 1)
                {
                    return _period;
                }

                // Second failure, start ramping up the interval - first 2x, then 4x, ...
                var backoffFactor = Math.Pow(2, (_failuresSinceSuccessfulBatch - 1));

                // If the period is ridiculously short, give it a boost so we get some
                // visible backoff.
                var backoffPeriod = Math.Max(_period.Ticks, MinimumBackoffPeriod.Ticks);

                // The "ideal" interval
                var backedOff = (long)(backoffPeriod * backoffFactor);

                // Capped to the maximum interval
                var cappedBackoff = Math.Min(MaximumBackoffInterval.Ticks, backedOff);

                // Unless that's shorter than the period, in which case we'll just apply the period
                var actual = Math.Max(_period.Ticks, cappedBackoff);

                return TimeSpan.FromTicks(actual);
            }
        }

        public bool ShouldDropBatch => _failuresSinceSuccessfulBatch >= FailuresBeforeDroppingBatch;

        public bool ShouldDropQueue => _failuresSinceSuccessfulBatch >= FailuresBeforeDroppingQueue;

        public void MarkSuccess()
        {
            _failuresSinceSuccessfulBatch = 0;
        }

        public void MarkFailure()
        {
            ++_failuresSinceSuccessfulBatch;
        }
    }
}
