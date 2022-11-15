using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(35, expectedNumberOfSnapshots: 0 /* Generic value type is not supported at the moment */)]
    internal class GenericInnerValueTypeTest : IRun
    {
        public void Run()
        {
            var place = new Place { @Type = PlaceType.City, Name = "New York" };
            var address = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>();
            children.Add(new Person("Ralph Jr.", 31, address, Guid.Empty, null));
            var person = new Person("Ralph", 99, address, Guid.Empty, children);

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
