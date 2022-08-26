// <copyright file="NullStatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal class NullStatsAggregator : IStatsAggregator
    {
        public bool? CanComputeStats => false;

        public void Add(params Span[] spans)
        {
        }

        public void AddRange(ArraySegment<Span> spans)
        {
        }

        public bool RunSamplers(ArraySegment<Span> spans) => true;

        public ArraySegment<Span> ProcessTrace(ArraySegment<Span> trace) => trace;

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
