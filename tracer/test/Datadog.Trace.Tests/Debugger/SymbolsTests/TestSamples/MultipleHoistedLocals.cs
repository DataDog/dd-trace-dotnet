// <copyright file="MultipleHoistedLocals.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class MultipleHoistedLocals
    {
        public void HoistedLocals()
        {
            int a = 10;
            int b = 20;
            string s = "Sum";

            Action action = () =>
            {
                int c = a + b;
                Console.WriteLine("Sum of a and b: " + c);
                Console.WriteLine("String is: " + s);
            };

            action.Invoke();
        }
    }
}
