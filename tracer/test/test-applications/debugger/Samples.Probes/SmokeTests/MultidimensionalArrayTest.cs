using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class MultidimensionalArrayTest : IRun
    {
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
