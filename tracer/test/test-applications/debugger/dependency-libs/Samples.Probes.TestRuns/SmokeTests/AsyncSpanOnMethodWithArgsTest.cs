using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 35)]
    [LogLineProbeTestData(lineNumber: 36)]
    [LogLineProbeTestData(lineNumber: 37)]
    [LogLineProbeTestData(lineNumber: 42)]
    public class AsyncSpanOnMethodWithArgsTest : IAsyncRun
    {
        private const string ClassName = "AsyncSpanOnMethodWithArgsTest";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            var place = new Place { Type = PlaceType.City, Name = "New York" };

            var adr = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>
            {
                new Person("Ralph Jr.", 31, adr, Guid.Empty, null)
            };
            var person = new Person("Ralph", 99, adr, Guid.Empty, children);
            await Method($"{ClassName}.{nameof(RunAsync)}", person);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SpanOnMethodProbeTestData]
        internal async Task<string> Method(string input, Person person)
        {
            var somewhat = input + person.Id;
            await Task.Yield();
            return Calculate(input, ref person) + person.Age.ToString() + somewhat;
        }

        private string Calculate(string input, ref Person person)
        {
            return input + person.Adrs.Street;
        }
    }
}
