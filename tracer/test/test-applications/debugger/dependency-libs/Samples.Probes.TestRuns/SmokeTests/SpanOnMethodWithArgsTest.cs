using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogOnLineProbeTestData(lineNumber: 32)]
    public class SpanOnMethodWithArgsTest : IRun
    {
        private const string ClassName = "SpanOnMethodWithArgsTest";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var place = new Place { Type = PlaceType.City, Name = "New York" };

            var address = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>
            {
                new Person("Ralph Jr.", 31, address, Guid.Empty, null)
            };
            var person = new Person("Ralph", 99, address, Guid.Empty, children);
            Method($"{ClassName}.{nameof(Run)}", ref person, person);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanOnMethodProbeTestData]
        internal string Method(string input, ref Person person, Person person2)
        {
            return Calculate(input, ref person) + person2.Age.ToString();
        }

        private string Calculate(string input, ref Person person)
        {
            return input + person.Address.Street;
        }
    }
}
