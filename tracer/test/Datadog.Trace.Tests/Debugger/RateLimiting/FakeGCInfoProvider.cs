// <copyright file="FakeGCInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.RateLimiting;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    /// <summary>
    /// Fake GC info provider for deterministic testing without real GC interactions
    /// </summary>
    internal class FakeGCInfoProvider : IGCInfoProvider
    {
        private readonly Queue<int> _gen2Counts = new();
        private readonly Queue<double> _memoryRatios = new();
        private double _currentMemoryRatio = 0.5;
        private int _currentGen2Count = 0;

        public FakeGCInfoProvider WithGen2Counts(params int[] counts)
        {
            foreach (var count in counts)
            {
                _gen2Counts.Enqueue(count);
            }

            return this;
        }

        public FakeGCInfoProvider WithMemoryRatios(params double[] ratios)
        {
            foreach (var ratio in ratios)
            {
                _memoryRatios.Enqueue(ratio);
            }

            return this;
        }

        public FakeGCInfoProvider WithConstantGen2Count(int count)
        {
            _currentGen2Count = count;
            return this;
        }

        public FakeGCInfoProvider WithConstantMemoryRatio(double ratio)
        {
            _currentMemoryRatio = ratio;
            return this;
        }

        public int GetGen2CollectionCount()
        {
            if (_gen2Counts.Count > 0)
            {
                _currentGen2Count = _gen2Counts.Dequeue();
            }

            return _currentGen2Count;
        }

#if NETCOREAPP3_1_OR_GREATER
        public GCMemoryInfo GetGCMemoryInfo()
        {
            // Return a default GCMemoryInfo - not used in current tests
            // but needed for interface completeness
            return new GCMemoryInfo();
        }
#endif

        public double GetMemoryUsageRatio()
        {
            if (_memoryRatios.Count > 0)
            {
                _currentMemoryRatio = _memoryRatios.Dequeue();
            }

            return _currentMemoryRatio;
        }
    }
}
