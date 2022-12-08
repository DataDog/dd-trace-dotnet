using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LineProbeTestData(23)]
    [LineProbeTestData(25)]
    [LineProbeTestData(26)]
    [LineProbeTestData(27)]
    [LineProbeTestData(28)]
    [LineProbeTestData(29)]
    [LineProbeTestData(45)]
    [LineProbeTestData(46)]
    [LineProbeTestData(47)]
    public class AsyncLineProbeWithFieldsArgsAndLocalsTest : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            var place = new Place { Type = PlaceType.City, Name = "New York" };

            var address = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>();
            children.Add(new Person("Ralph Jr.", 31, address, Guid.Empty, null));
            var person = new Person("Ralph", 99, address, Guid.Empty, children);
            await new NestedAsyncGenericStruct(person).Method(new Generic { Message = "NestedAsyncGenericStruct" }, $".{nameof(RunAsync)}", person);
        }

        internal class NestedAsyncGenericStruct
        {
            private readonly Person _person;

            public NestedAsyncGenericStruct(Person person)
            {
                _person = person;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            [MethodProbeTestData]
            public async Task<string> Method(Generic someGenericObject, string input, Person goodPerson)
            {
                var output = goodPerson.ToString() + someGenericObject.ToString() + goodPerson.Name;
                await Task.Delay(20);
                return output + nameof(Method) + _person.Age + _person.Name;
            }
        }
    }
}
