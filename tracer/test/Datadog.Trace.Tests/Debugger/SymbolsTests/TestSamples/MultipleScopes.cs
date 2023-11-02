// <copyright file="MultipleScopes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class MultipleScopes
    {
        public void LocalScopes()
        {
            // First local scope
            {
                // First local variable
                int a = 10;
                Console.WriteLine("First local scope: " + a);

                // Second local variable
                int b = 20;
                Console.WriteLine("First local scope: " + b);

                // Accessing local variables within the same scope
                int c = a + b;
                Console.WriteLine("First local scope: " + c);
            }

            // Second local scope
            {
                // Third local variable
                int d = 30;
                Console.WriteLine("Second local scope: " + d);

                // Fourth local variable
                int e = 40;
                Console.WriteLine("Second local scope: " + e);

                // Accessing local variables within the same scope
                int f = d + e;
                Console.WriteLine("Second local scope: " + f);
            }

            // Third local scope
            {
                // Fifth local variable
                int g = 50;
                Console.WriteLine("Third local scope:" + g);
                // Accessing local variables within the same scope
                int h = g * 2;
                Console.WriteLine("Third local scope: " + h);
            }

            // Fourth local scope
            {
                // Sixth local variable
                string i = "Hello";
                Console.WriteLine("Fourth local scope: " + i);

                // Seventh local variable
                string j = "World";
                Console.WriteLine("Fourth local scope: " + j);

                // Accessing local variables within the same scope
                string k = i + " " + j;
                Console.WriteLine("Fourth local scope: " + k);
            }
        }
    }
}
