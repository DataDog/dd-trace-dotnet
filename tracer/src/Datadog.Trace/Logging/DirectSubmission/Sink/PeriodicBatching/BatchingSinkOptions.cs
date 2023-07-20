// <copyright file="BatchingSinkOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// based on https://github.com/serilog/serilog-sinks-periodicbatching/blob/66a74768196758200bff67077167cde3a7e346d5/src/Serilog.Sinks.PeriodicBatching/Sinks/PeriodicBatching/PeriodicBatchingSinkOptions.cs
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
#nullable enable

using System;

namespace Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching
{
    internal class BatchingSinkOptions
    {
        public BatchingSinkOptions(
            int batchSizeLimit,
            int queueLimit,
            TimeSpan period)
         : this(
             batchSizeLimit,
             queueLimit,
             period,
             circuitBreakPeriod: TimeSpan.FromTicks(period.Ticks),
             failuresBeforeCircuitBreak: 10)
        {
        }

        public BatchingSinkOptions(
            int batchSizeLimit,
            int queueLimit,
            TimeSpan period,
            TimeSpan circuitBreakPeriod,
            int failuresBeforeCircuitBreak)
        {
            BatchSizeLimit = batchSizeLimit;
            QueueLimit = queueLimit;
            Period = period;
            CircuitBreakPeriod = circuitBreakPeriod;
            FailuresBeforeCircuitBreak = failuresBeforeCircuitBreak;
        }

        /// <summary>
        /// Gets the maximum number of events to include in a single batch.
        /// </summary>
        public int BatchSizeLimit { get; }

        /// <summary>
        /// Gets the time to wait between checking for event batches.
        /// </summary>
        public TimeSpan Period { get; }

        /// <summary>
        /// Gets the time to ignore logs when repeated failures to emit a batch cause the circuit to break.
        /// </summary>
        public TimeSpan CircuitBreakPeriod { get; }

        /// <summary>
        /// Gets maximum number of events to hold in the sink's internal queue
        /// </summary>
        public int QueueLimit { get; }

        /// <summary>
        /// Gets the number of failures to emit the batch before the circuit breaker breaks
        /// </summary>
        public int FailuresBeforeCircuitBreak { get; }
    }
}
