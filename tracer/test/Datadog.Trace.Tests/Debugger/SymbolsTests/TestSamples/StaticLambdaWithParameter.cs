// <copyright file="StaticLambdaWithParameter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class StaticLambdaWithParameter
    {
        public static void Foo()
        {
            var threshold = 10;
            Func<int, bool> predicate = i => i > threshold;
            Console.WriteLine(predicate(11));
        }
    }
}
