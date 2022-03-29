// <copyright file="TimingTests.cs" company="Datadog">
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
    public class TimingTests
    {
        private const int _iterations = 1000;

        [Fact]
        public void CheckWithOneThread()
        {
            var timing = new Timing();
            double expectedValue = _iterations * (_iterations + 1) / 2;
            for (int i = 1; i <= _iterations; i++)
            {
                timing.Time(i);
            }

            Assert.Equal(expectedValue, timing.GetTime());
        }

        [Fact]
        public async Task CheckWithMultipleThreads()
        {
            const int max = 200;

            ThreadPool.SetMinThreads(_iterations / 10, 10);

            var timing = new Timing();
            var parallelActions = new List<Task>(_iterations);
            var valuesSet = new List<List<double>>(_iterations);
            double expectedSum = 0;
            double localSum;

            for (int i = 0; i < _iterations; i++)
            {
                valuesSet.Add(Helpers.GetRandomDoubles(_iterations, max, out _, out localSum));
                expectedSum += localSum;
            }

            for (int i = 0; i < _iterations; i++)
            {
                parallelActions.Add(
                    Task.Factory.StartNew(
                        (parameters) =>
                        {
                            var currentSet = (int)parameters;
                            for (int j = 0; j < _iterations; j++)
                            {
                                timing.Time(valuesSet[currentSet][j]);
                            }
                        },
                        i));
            }

            await Task.WhenAll(parallelActions);

            Assert.Equal(expectedSum, timing.GetTime());
        }
    }
}
