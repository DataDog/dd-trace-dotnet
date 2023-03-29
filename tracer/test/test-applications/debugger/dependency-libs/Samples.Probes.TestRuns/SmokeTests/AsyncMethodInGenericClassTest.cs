using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncMethodInGenericClassTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            var person = new Person("Harry", 28, new Address(), Guid.Empty, null);
            await new GenericClass<Person>().Run(person);
            var address = new Address { City = new Place { Name = "Some Place", Type = PlaceType.City }, HomeType = BuildingType.Cottage, Number = 99, Street = "Wall" };
            await new GenericClass<Address>().Run(address);
        }
    }

    internal class GenericClass<T>
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogOnMethodProbeTestData]
        public async Task Run(T t)
        {
            var def = default(T);
            await Task.Yield();
            Console.WriteLine(def);
        }
    }
}
