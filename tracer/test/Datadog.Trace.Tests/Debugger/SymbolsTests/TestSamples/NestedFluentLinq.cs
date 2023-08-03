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
            var enumerable = Enumerable.Range(0, 10).Where(i => i % 2 == 0).Select(i => i + 1).Join(Enumerable.Range(0, 10).Where(i => i % 2 == 0), i => i, i => i, (i, i1) => i + i1);
            Console.WriteLine(enumerable.ToList());
        }
    }
}
