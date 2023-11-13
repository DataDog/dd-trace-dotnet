// <copyright file="NestedFluentLinq.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class NestedFluentLinq
    {
        private void Foo()
        {
            var enumerable = Enumerable.Range(0, 10).
                                        Where(a => a % 2 == 0).
                                        Select(b => b + 1).
                                        Join(
                                            Enumerable.Range(0, 10).
                                                       Where(c => c % 2 == 0),
                                            d => d,
                                            e => e,
                                            (f, g) => f + g);
            Console.WriteLine(enumerable.ToList());
        }
    }
}
