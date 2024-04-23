using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncWithGenericArgumentAndLocal : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await new AsyncWithGenericArgumentAndLocal.NestedAsyncGenericClass<Generic>().Method(new NestedAsyncGenericClass<Generic> { Generic = new Generic() { Message = nameof(AsyncWithGenericArgumentAndLocal) } }, $".{nameof(RunAsync)}");
        }

        internal class BaseClass<T> where T : IGeneric, new()
        {
            public T BaseMessage { get; set; } = new T();
        }

        internal class NestedAsyncGenericClass<T> : BaseClass<Generic> where T : IGeneric, new()
        {
            public T Generic { get; set; }

            enum State
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
            private static State[][] _stateArray;

            [MethodImpl(MethodImplOptions.NoInlining)]
            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /*in optimize code this will create a nested struct inside generic parent*/, expectProbeStatusFailure: true)]
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
