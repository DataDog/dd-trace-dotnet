// <copyright file="ValueTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.RuntimeMetrics;
using Xunit;

namespace RuntimeMetrics.Tests
{
    public class ValueTests
    {
        private const int _iterations = 1000;

        [Fact]
        public void CheckWithOneThread()
        {
            var value = new Value();
            var expectedMax = _iterations;
            for (int i = 1; i <= _iterations; i++)
            {
                value.Add(i);
            }

            Assert.Equal(expectedMax, value.GetMax());
        }

        [Fact]
        public async Task CheckWithMultipleThreads()
        {
            const int max = 200;

            ThreadPool.SetMinThreads(_iterations / 10, 10);

            var value = new Value();
            var parallelActions = new List<Task>(_iterations);
            var valuesSet = new List<List<long>>(_iterations);
            long expectedMaxValue = 0;
            long localMax;
            long localSum;

            for (int i = 0; i < _iterations; i++)
            {
                valuesSet.Add(Helpers.GetRandomNumbers(_iterations, max, out localMax, out localSum));
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
                                value.Add(valuesSet[currentSet][j]);
                            }
                        },
                        i));
            }

            await Task.WhenAll(parallelActions);

            Assert.Equal(expectedMaxValue, value.GetMax());
        }
    }
}
