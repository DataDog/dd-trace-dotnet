// <copyright file="AsyncWithGenericArgumentAndLocal.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    internal class AsyncWithGenericArgumentAndLocal : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await new AsyncWithGenericArgumentAndLocal.NestedAsyncGenericClass<Generic>().Method(new NestedAsyncGenericClass<Generic> { Generic = new Generic() { Message = nameof(AsyncWithGenericArgumentAndLocal) } }, $".{nameof(RunAsync)}");
        }

        internal class BaseClass<T>
            where T : IGeneric, new()
        {
            public T BaseMessage { get; set; } = new T();
        }

        internal class NestedAsyncGenericClass<T> : BaseClass<Generic>
            where T : IGeneric, new()
        {
            // array that gives a new state based on the current state an the token being written
            private static State[][] _stateArray;

            private enum State
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

            public T Generic { get; set; }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData]
            public async Task<string> Method(NestedAsyncGenericClass<T> generic, string input)
            {
                var list = new List<T> { new T() };
                _stateArray = new State[1][];
                _stateArray[0] = new State[1];
                _stateArray[0][0] = State.Array;
                await Task.Delay(20);
                var output = generic.Generic.Message + input + "." + BaseMessage.Message + ".";
                return output + nameof(Method) + list.First().ToString() + " - " + Enum.GetName(typeof(State), _stateArray[0][0]);
            }
        }
    }
}
