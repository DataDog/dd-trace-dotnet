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

        public bool Add(params Span[] spans) => false;

        public bool AddRange(ArraySegment<Span> spans) => false;

        public ArraySegment<Span> ProcessTrace(ArraySegment<Span> trace) => trace;

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
