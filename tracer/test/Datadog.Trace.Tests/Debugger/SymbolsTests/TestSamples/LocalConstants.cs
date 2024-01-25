// <copyright file="LocalConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class LocalConstants
    {
        internal void Foo(double arg)
        {
            double result = 0;
            if (arg > 0)
            {
                const string foo = nameof(Foo);
                var number = Math.Pow(arg, 2);
                result = number + arg;
                Print(result, number, foo);
            }
            else
            {
                const string bar = "Bar";
                Print(result, 0, bar);
            }
        }

        private void Print(double result, double number, string name)
        {
            Console.WriteLine($"{name}: {result + number}");
        }
    }
}
