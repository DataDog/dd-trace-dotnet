// <copyright file="MockApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.TestHelpers
{
    internal class MockApi : IApi
    {
        private readonly ManualResetEventSlim _resetEvent;
        private readonly object _lock = new();
        private List<List<MockSpan>> _objects = new();

        public MockApi(ManualResetEventSlim resetEvent = null)
        {
            _resetEvent = resetEvent ?? new();
        }

        public List<List<MockSpan>> Traces
        {
            get
            {
                lock (_lock)
                {
                    return _objects;
                }
            }
        }

        public List<List<MockSpan>> Wait(TimeSpan? timeout = null)
        {
            _resetEvent.Wait(timeout ?? TimeSpan.FromMinutes(1));
            var objects = Traces;
            _resetEvent.Reset();
            return objects;
        }

        public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool appsecStandaloneEnabled)
        {
            // use nuget MessagePack to deserialize
            var spans = global::MessagePack.MessagePackSerializer.Deserialize<List<List<MockSpan>>>(traces);

            if (spans.Count > 0)
            {
                lock (_lock)
                {
                    var previous = _objects;
                    _objects = previous.Concat(spans).ToList();
                }

                _resetEvent.Set();
            }

            return Task.FromResult(true);
        }

        public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
        {
            throw new NotImplementedException();
        }
    }
}
