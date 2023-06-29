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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Datadog.Trace.Util
{
    internal class BoundedConcurrentQueue<T>
    {
        private readonly ConcurrentQueue<T> _queue = new();
        private readonly int _queueLimit;

        private int _counter;

        public BoundedConcurrentQueue(int queueLimit)
        {
            if (queueLimit is <= 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(queueLimit), "Queue limit must be positive, or `null` to indicate unbounded.");
            }

            _queueLimit = queueLimit;
        }

        // Internal for testing
        internal ConcurrentQueue<T> InnerQueue => _queue;

        public int Count => _counter;

        public bool IsEmpty => _queue.IsEmpty;

        public bool TryDequeue([NotNullWhen(returnValue: true)] out T? item)
        {
            if (_queue.TryDequeue(out item!))
            {
                Interlocked.Decrement(ref _counter);
                return true;
            }

            return false;
        }

        public bool TryEnqueue(T item)
        {
            if (Interlocked.Increment(ref _counter) <= _queueLimit)
            {
                _queue.Enqueue(item);
                return true;
            }

            Interlocked.Decrement(ref _counter);
            return false;
        }

        public T[] ToArray() => _queue.ToArray();

        /// <summary>
        /// Remove all the items from the queue. Note that this is NOT thread safe,
        /// and should not be called at the same time as <see cref="TryDequeue"/> or <see cref="TryEnqueue"/>
        /// </summary>
        public void Clear()
        {
#if NETCOREAPP
            _queue.Clear();
#else
            while (_queue.TryDequeue(out _))
            {
            }
#endif
            _counter = 0;
        }
    }
}
