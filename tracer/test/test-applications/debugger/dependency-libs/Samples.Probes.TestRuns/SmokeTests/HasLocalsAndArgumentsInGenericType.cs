using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class HasLocalsAndArgumentsInGenericNestedType : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            new Test<GenericInstantiation>().Method(new GenericInstantiation(), 36);
        }

        private class GenericInstantiation
        {
            public GenericInstantiation()
            {
            }

            public override string ToString()
            {
                return $"{nameof(GenericInstantiation)}!";
            }
        }

        public class Test<T> where T : new()
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            [LogMethodProbeTestData("System.String", new[] { "!0", "System.Int32" })]
            public string Method(T genericVar, int age)
            {
                var genericVarToString = genericVar.ToString() + "Simon";
                genericVarToString = genericVarToString.Length > 5 ? genericVarToString : genericVarToString + "Simon";
                var p2 = new Person(genericVarToString, 30, new Address { HomeType = BuildingType.Hotel, Number = 3, Street = "Elsewhere" }, System.Guid.NewGuid(), null);
                var newT = (new T()).ToString() + "Lucy";
                newT = newT.Length > 4 ? newT : newT + "Lucy";
                var p3 = new Person(newT, 7.5, new Address { HomeType = BuildingType.House, Number = 100, Street = "Here" }, System.Guid.NewGuid(), null);
                var children = new System.Collections.Generic.List<Person> { p2, p3 };
                var p = new Person(newT, age, new Address { HomeType = BuildingType.Cottage, Number = 17, Street = "Somewhere" }, System.Guid.NewGuid(), children);
                return $"Hello {p}!";
            }
        }
    }
}
