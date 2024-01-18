// <copyright file="ComprehensiveLinqWithoutClosure.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class ComprehensiveLinqWithoutClosure
    {
        private void Foo()
        {
            int rand = new Random().Next();
            Console.WriteLine(rand);

            var numbers = from i in Enumerable.Range(0, 10)
                          where i % 2 == 0
                          select i;

            Console.WriteLine(numbers);
        }
    }
}
