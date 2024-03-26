using System;
using System.Collections.Generic;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(23)]
    [LogLineProbeTestData(24)]
    [LogLineProbeTestData(25)]
    [LogLineProbeTestData(26)]
    [LogLineProbeTestData(27)]
    [LogLineProbeTestData(29)]
    [LogLineProbeTestData(30)]
    [LogLineProbeTestData(31)]
    [LogLineProbeTestData(32)]
    [LogLineProbeTestData(34)]
    [LogLineProbeTestData(35)]
    [LogLineProbeTestData(50, expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */, expectProbeStatusFailure: true)]
    public class GenericByRefLikeTest : IRun
    {
        public void Run()
        {
            var place = new Place { Type = PlaceType.City, Name = "New York" };
            var adr = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>();
            children.Add(new Person("Ralph Jr.", 31, adr, Guid.Empty, null));
            var person = new Person("Ralph", 99, adr, Guid.Empty, children);

            var genericByRefLike = new GenericByRefLike<Person>(person);
            genericByRefLike.CallMe("Hello from the outside 1!", genericByRefLike, ref genericByRefLike);
            genericByRefLike.CallMe2("Hello from the outside 2!", genericByRefLike, ref genericByRefLike);
            genericByRefLike.CallMe3("Hello from the outside 3!", genericByRefLike, ref genericByRefLike);

            var genericByRefLike2 = new GenericByRefLike<Address>(adr);
            genericByRefLike.CallMe4<Address>("Hello from the outside 3!", genericByRefLike2, ref genericByRefLike2);
        }
        
        ref struct GenericByRefLike<T>
        {
            private T _whoAmI;

            public GenericByRefLike(T whoAmI)
            {
                _whoAmI = whoAmI;
            }

            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */, expectProbeStatusFailure: true)]
            public ref GenericByRefLike<T> CallMe(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return ref refByRefLike;
            }

            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */, expectProbeStatusFailure: true)]
            public GenericByRefLike<T> CallMe2(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return byRefLike;
            }

            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */, expectProbeStatusFailure: true)]
            public string CallMe3(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return @in + "Hello World";
            }

            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */, expectProbeStatusFailure: true)]
            public string CallMe4<K>(string @in, GenericByRefLike<K> byRefLike, ref GenericByRefLike<K> refByRefLike)
            {
                return @in + "Hello World";
            }
        }
    }
}
