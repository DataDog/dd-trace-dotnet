using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    // https://datadoghq.atlassian.net/browse/DEBUG-723
    public class HasLocalListOfObjects : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("Greg", 36);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.String", new[] { "System.String", "System.Int32" }, skipOnFrameworks: "net6.0")]
        public string Method(string name, int age)
        {
            var p2 = new Person("Simon", 30, new Address { HomeType = BuildingType.Hotel, Number = 3, Street = "Elsewhere" }, System.Guid.NewGuid(), null);
            var p3 = new Person("Lucy", 7.5, new Address { HomeType = BuildingType.House, Number = 100, Street = "Here" }, System.Guid.NewGuid(), null);
            var children = new System.Collections.Generic.List<Person> { p2, p3 };
            var p = new Person(name, age, new Address { HomeType = BuildingType.Cottage, Number = 17, Street = "Somewhere" }, System.Guid.NewGuid(), children);
            return $"Hello {p}!";
        }
    }
}
