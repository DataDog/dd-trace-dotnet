// <copyright file="BoundedConcurrentQueue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Based on https://github.com/serilog/serilog-sinks-periodicbatching/blob/66a74768196758200bff67077167cde3a7e346d5/src/Serilog.Sinks.PeriodicBatching/Sinks/PeriodicBatching/BoundedConcurrentQueue.cs
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
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching
{
    internal class BoundedConcurrentQueue<T>
    {
        private const int Unbounded = -1;

        private readonly ConcurrentQueue<T> _queue = new();
        private readonly int _queueLimit;

        private int _counter;

        public BoundedConcurrentQueue(int? queueLimit = null)
        {
            if (queueLimit is <= 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(queueLimit), "Queue limit must be positive, or `null` to indicate unbounded.");
            }

            _queueLimit = queueLimit ?? Unbounded;
        }

        public int Count => _queue.Count;

        public bool TryDequeue([NotNullWhen(returnValue: true)] out T? item)
        {
            if (_queueLimit == Unbounded)
            {
                return _queue.TryDequeue(out item!);
            }

            var result = false;
            try
            { }
            finally
            {
                // prevent state corrupt while aborting
                if (_queue.TryDequeue(out item!))
                {
                    Interlocked.Decrement(ref _counter);
                    result = true;
                }
            }

            return result;
        }

        public bool TryEnqueue(T item)
        {
            if (_queueLimit == Unbounded)
            {
                _queue.Enqueue(item);
                return true;
            }

            var result = true;
            try
            { }
            finally
            {
                if (Interlocked.Increment(ref _counter) <= _queueLimit)
                {
                    _queue.Enqueue(item);
                }
                else
                {
                    Interlocked.Decrement(ref _counter);
                    result = false;
                }
            }

            return result;
        }
    }
}
