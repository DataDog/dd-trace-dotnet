using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(39)]
    internal class AsyncGenericMethodWithLineProbeTest : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            var place = new Place { @Type = PlaceType.City, Name = "New York" };

            var address = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>();
            children.Add(new Person("Ralph Jr.", 31, address, Guid.Empty, null));
            var person = new Person("Ralph", 99, address, Guid.Empty, children);
            await new NestedAsyncGenericStruct<Generic>(person).Method(new Generic { Message = "NestedAsyncGenericStruct" }, $".{nameof(RunAsync)}");
        }

        internal struct NestedAsyncGenericStruct<T> where T : IGeneric
        {
            private readonly Person _person;

            public NestedAsyncGenericStruct(Person person)
            {
                _person = person;
            }

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData(expectedNumberOfSnapshots: 0 /* Async Method Probes are disabled for now. */)]
            public async Task<string> Method<K>(K generic, string input) where K : IGeneric
            {
                var output = generic.Message + input + ".";
                await Task.Delay(20);
                return output + nameof(Method);
            }
        }
    }
}
