// <copyright file="PartialClassPartTwo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal partial class PartialClass
    {
        private readonly string _s2;

        private void Bar(int i)
        {
            var rand = new Random();
            var next = rand.Next();
            Console.WriteLine($"{_s2} - {i} - {next}");
        }
    }
}
