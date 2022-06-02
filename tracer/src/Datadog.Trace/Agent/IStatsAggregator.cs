// <copyright file="IStatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IStatsAggregator
    {
        void Add(params Span[] spans);

        void AddRange(Span[] spans, int offset, int count);

        Task DisposeAsync();
    }
}
