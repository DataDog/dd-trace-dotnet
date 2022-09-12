// <copyright file="MultidimensionalArrayTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class MultidimensionalArrayTest : IRun
    {
        // array that gives a new state based on the current state an the token being written
        private static readonly State[][] StateArray;

        static MultidimensionalArrayTest()
        {
            const int size = 2;

            StateArray = new State[size][];

            for (var i = 0; i < size; i++)
            {
                _ = Enum.TryParse<State>("9", true, out var val);

                StateArray[i] = new[] { val, val, val, val, val, val, val, val, val, val };
            }
        }

        internal enum State
        {
            Start = 0,
            Property = 1,
            ObjectStart = 2,
            Object = 3,
            ArrayStart = 4,
            Array = 5,
            ConstructorStart = 6,
            Constructor = 7,
            Closed = 8,
            Error = 9
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public void Method()
        {
        }
    }
}
