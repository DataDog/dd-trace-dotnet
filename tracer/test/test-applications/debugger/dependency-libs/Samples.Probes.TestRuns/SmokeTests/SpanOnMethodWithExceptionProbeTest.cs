using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 38)]
    [LogLineProbeTestData(lineNumber: 33)]
    public class SpanOnMethodWithExceptionProbeTest : IRun
    {
        private const string ClassName = "SpanOnMethodWithExceptionProbeTest";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var place = new Place { Type = PlaceType.City, Name = "New York" };

            var adr = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>
            {
                new Person("Ralph Jr.", 31, adr, Guid.Empty, null)
            };
            var person = new Person("Ralph", 99, adr, Guid.Empty, children);
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
            throw new InvalidOperationException(person.Name);
        }
    }
}
