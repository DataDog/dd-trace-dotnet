// <copyright file="StaticHoistedLocalsInStateMachine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples
{
    internal class StaticHoistedLocalsInStateMachine
    {
        public static async Task DoAsyncWork()
        {
            int a = 10;
            int b = 20;

            Console.WriteLine("Before awaiting: " + (a + b));

            await Task.Delay(10);

            Console.WriteLine("After awaiting: " + (a + b));
        }
    }
}
