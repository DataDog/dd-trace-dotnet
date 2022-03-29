// <copyright file="CounterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.RuntimeMetrics;
using Xunit;

namespace RuntimeMetrics.Tests
{
    public class CounterTests
    {
        private const int _iterations = 1000;

        [Fact]
        public void CheckWithOneThread()
        {
            var counter = new Counter();
            var expectedValue = _iterations;
            for (int i = 0; i < _iterations; i++)
            {
                counter.Inc();
            }

            Assert.Equal(expectedValue, counter.GetValue());
        }

        [Fact]
        public async Task CheckWithMultipleThreads()
        {
            ThreadPool.SetMinThreads(_iterations / 10, 10);

            var counter = new Counter();
            var parallelActions = new List<Task>(_iterations);
            int expectedValue = _iterations * _iterations;

            for (int i = 0; i < _iterations; i++)
            {
                parallelActions.Add(
                    Task.Run(() =>
                    {
                        for (int j = 0; j < _iterations; j++)
                        {
                            counter.Inc();
                        }
                    }));
            }

            await Task.WhenAll(parallelActions);

            Assert.Equal(expectedValue, counter.GetValue());
        }
    }
}
