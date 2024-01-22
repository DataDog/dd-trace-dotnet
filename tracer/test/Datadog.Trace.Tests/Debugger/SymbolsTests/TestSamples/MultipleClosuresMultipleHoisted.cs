// <copyright file="MultipleClosuresMultipleHoisted.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class MultipleClosuresMultipleHoisted
    {
        public void Foo(int argument1, int argument2)
        {
            var res = Enumerable.Range(0, 10).
                                 Where(t1 => t1 % 2 == 0).
                                 Select(t2 => t2 + 1).
                                 Join(
                                     Enumerable.Range(0, 5),
                                     i1 => i1 + 2,
                                     i2 => i2,
                                     delegate(int i3, int i4) { return i3 + i4; }).
                                 Count(i5 => i5 < argument1);
            if (res > 5)
            {
                Console.WriteLine(argument2);
            }

            Console.WriteLine(argument1);
        }
    }
}
