// <copyright file="Helpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace RuntimeMetrics.Tests
{
    public static class Helpers
    {
        /// <summary>
        /// Fill up a list of integer with random values up to limit
        /// </summary>
        /// <param name="count">number of integers in the list</param>
        /// <param name="limit">upper value in the list</param>
        /// <param name="localMax">return the max value in the list</param>
        /// <param name="localSum">return the sum of all values in the list</param>
        /// <returns>a List of random integer</returns>
        public static List<long> GetRandomNumbers(int count, int limit, out long localMax, out long localSum)
        {
            var result = new List<long>(count);
            var randomizer = new Random(DateTime.Now.Millisecond);
            localMax = 0;
            localSum = 0;
            for (int i = 0; i < count; i++)
            {
                var v = randomizer.Next(limit + 1);
                result.Add(v);
                localSum += v;
                if (v > localMax)
                {
                    localMax = v;
                }
            }

            return result;
        }

        /// <summary>
        /// Fill up a list of double with random values up to limit
        /// </summary>
        /// <param name="count">number of double in the list</param>
        /// <param name="limit">upper value in the list</param>
        /// <param name="localMax">return the max value in the list</param>
        /// <param name="localSum">return the sum of all values in the list</param>
        /// <returns>a List of random double</returns>
        public static List<double> GetRandomDoubles(int count, int limit, out double localMax, out double localSum)
        {
            var result = new List<double>(count);
            var randomizer = new Random(DateTime.Now.Millisecond);
            localMax = 0;
            localSum = 0;
            for (int i = 0; i < count; i++)
            {
                var v = randomizer.Next(limit + 1);
                result.Add(v);
                localSum += v;
                if (v > localMax)
                {
                    localMax = v;
                }
            }

            return result;
        }
    }
}
