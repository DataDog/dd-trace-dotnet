using System;
using System.Collections.Generic;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(35, expectedNumberOfSnapshots: 0 /* Generic value type is not supported at the moment */, expectProbeStatusFailure: true)]
    public class GenericInnerValueTypeTest : IRun
    {
        public void Run()
        {
            var place = new Place { Type = PlaceType.City, Name = "New York" };
            var adr = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>();
            children.Add(new Person("Ralph Jr.", 31, adr, Guid.Empty, null));
            var person = new Person("Ralph", 99, adr, Guid.Empty, children);

            new InnerGenericStruct<Person>(person).InstrumentMe();
        }

        internal struct InnerGenericStruct<T>
        {
            private T _t;

            public InnerGenericStruct(T t)
            {
                _t = t;
            }

            public string InstrumentMe()
            {
                return _t.ToString();
            }
        }
    }
}
