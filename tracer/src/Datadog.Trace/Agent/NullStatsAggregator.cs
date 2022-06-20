// <copyright file="NullStatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal class NullStatsAggregator : IStatsAggregator
    {
        public void Add(params Span[] spans)
        {
        }

        public void AddRange(Span[] spans, int offset, int count)
        {
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
