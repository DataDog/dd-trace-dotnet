using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(41,
                          expectedNumberOfSnapshots:0 /* in optimize code this will create a generic struct state machine */, 
                          expectProbeStatusFailure: true, skipOnFrameworks: ["net5.0", "net48", "net462", "netcoreapp3.1", "netcoreapp3.0", "netcoreapp2.1"])]
    public class AsyncGenericMethodWithLineProbeTest : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            var place = new Place { Type = PlaceType.City, Name = "New York" };

            var adrs = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>();
            children.Add(new Person("Ralph Jr.", 31, adrs, Guid.Empty, null));
            var person = new Person("Ralph", 99, adrs, Guid.Empty, children);
            await new NestedAsyncGenericStruct<Generic>(person).Method(new Generic { Message = "NestedAsyncGenericStruct" }, $".{nameof(RunAsync)}");
        }

        internal struct NestedAsyncGenericStruct<T> where T : IGeneric
        {
            private readonly Person _person;

            public NestedAsyncGenericStruct(Person person)
            {
                _person = person;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* in optimize code this will create a generic struct state machine*/,
                                    expectProbeStatusFailure: false, /* this currently doesn't get reported as a failure, though it should */
                                    skipOnFrameworks: ["net5.0", "net48", "net462", "netcoreapp3.1", "netcoreapp3.0", "netcoreapp2.1"])]
            public async Task<string> Method<K>(K generic, string input) where K : IGeneric
            {
                var output = generic.Message + input + ".";
                await Task.Delay(20);
                return output + nameof(Method);
            }
        }
    }
}
