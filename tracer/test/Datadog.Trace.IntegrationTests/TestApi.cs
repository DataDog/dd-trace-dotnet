// <copyright file="TestApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.TestHelpers;
using MessagePack; // use nuget MessagePack to deserialize

namespace Datadog.Trace.IntegrationTests
{
    internal class TestApi : IApi
    {
        private readonly ManualResetEventSlim _resetEvent = new();
        private List<List<MockSpan>> _objects = null;

        public List<List<MockSpan>> Wait()
        {
            _resetEvent.Wait();
            var objects = Interlocked.Exchange(ref _objects, null);
            _resetEvent.Reset();
            return objects;
        }

        public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces)
        {
            var spans = MessagePackSerializer.Deserialize<List<List<MockSpan>>>(traces);

            if (spans.Count > 0)
            {
                var previous = Interlocked.Exchange(ref _objects, null);
                if (previous is not null)
                {
                    Interlocked.Exchange(ref _objects, previous.Concat(spans).ToList());
                }
                else
                {
                    Interlocked.Exchange(ref _objects, spans);
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
