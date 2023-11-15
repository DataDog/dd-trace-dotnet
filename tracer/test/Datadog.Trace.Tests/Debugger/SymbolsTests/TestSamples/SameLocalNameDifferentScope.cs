// <copyright file="SameLocalNameDifferentScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class SameLocalNameDifferentScope
    {
        internal void Foo(double arg)
        {
            double result = 0;
            if (arg > 0)
            {
                var number = Math.Pow(arg, 2);
                result = number + arg;
                Print(result, number);
            }
            else
            {
                var number = Math.Abs(arg);
                result = number + arg;
                Print(result, number);
            }
        }

        private void Print(double result, double number)
        {
            Console.WriteLine(result + number);
        }
    }
}
