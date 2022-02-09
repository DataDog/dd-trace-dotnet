// <copyright file="IncValueTests.cs" company="Datadog">
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
    public class IncValueTests
    {
        private const int _iterations = 1000;

        [Fact]
        public void CheckWithOneThread()
        {
            var incValue = new IncValue();
            var expectedMax = _iterations;
            var expectedSum = _iterations * (_iterations + 1) / 2;
            for (int i = 1; i <= _iterations; i++)
            {
                incValue.Add(i);
            }

            Assert.Equal(expectedMax, incValue.GetMax());
            Assert.Equal(expectedSum, incValue.GetSum());
        }

        [Fact]
        public async Task CheckWithMultipleThreads()
        {
            const int max = 200;

            ThreadPool.SetMinThreads(_iterations / 10, 10);

            var incValue = new IncValue();
            var parallelActions = new List<Task>(_iterations);
            var valuesSet = new List<List<double>>(_iterations);
            double expectedMaxValue = 0;
            double expectedSum = 0;
            double localMax;
            double localSum;

            for (int i = 0; i < _iterations; i++)
            {
                valuesSet.Add(Helpers.GetRandomDoubles(_iterations, max, out localMax, out localSum));
                expectedSum += localSum;
                if (expectedMaxValue < localMax)
                {
                    expectedMaxValue = localMax;
                }
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
                                incValue.Add(valuesSet[currentSet][j]);
                            }
                        },
                        i));
            }

            await Task.WhenAll(parallelActions);

            Assert.Equal(expectedMaxValue, incValue.GetMax());
            Assert.Equal(expectedSum, incValue.GetSum());
        }
    }
}
