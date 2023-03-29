using System;
using System.Collections.Generic;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogOnLineProbeTestData(23)]
    [LogOnLineProbeTestData(24)]
    [LogOnLineProbeTestData(25)]
    [LogOnLineProbeTestData(26)]
    [LogOnLineProbeTestData(27)]
    [LogOnLineProbeTestData(29)]
    [LogOnLineProbeTestData(30)]
    [LogOnLineProbeTestData(31)]
    [LogOnLineProbeTestData(32)]
    [LogOnLineProbeTestData(34)]
    [LogOnLineProbeTestData(35)]
    [LogOnLineProbeTestData(50, expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */)]
    public class GenericByRefLikeTest : IRun
    {
        public void Run()
        {
            var place = new Place { Type = PlaceType.City, Name = "New York" };
            var address = new Address { City = place, HomeType = BuildingType.Duplex, Number = 15, Street = "Harlem" };
            var children = new List<Person>();
            children.Add(new Person("Ralph Jr.", 31, address, Guid.Empty, null));
            var person = new Person("Ralph", 99, address, Guid.Empty, children);

            var genericByRefLike = new GenericByRefLike<Person>(person);
            genericByRefLike.CallMe("Hello from the outside 1!", genericByRefLike, ref genericByRefLike);
            genericByRefLike.CallMe2("Hello from the outside 2!", genericByRefLike, ref genericByRefLike);
            genericByRefLike.CallMe3("Hello from the outside 3!", genericByRefLike, ref genericByRefLike);

            var genericByRefLike2 = new GenericByRefLike<Address>(address);
            genericByRefLike.CallMe4<Address>("Hello from the outside 3!", genericByRefLike2, ref genericByRefLike2);
        }
        
        ref struct GenericByRefLike<T>
        {
            private T _whoAmI;

            public GenericByRefLike(T whoAmI)
            {
                _whoAmI = whoAmI;
            }

            [LogOnMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */)]
            public ref GenericByRefLike<T> CallMe(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return ref refByRefLike;
            }

            [LogOnMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */)]
            public GenericByRefLike<T> CallMe2(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return byRefLike;
            }

            [LogOnMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */)]
            public string CallMe3(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return @in + "Hello World";
            }

            [LogOnMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */)]
            public string CallMe4<K>(string @in, GenericByRefLike<K> byRefLike, ref GenericByRefLike<K> refByRefLike)
            {
                return @in + "Hello World";
            }
        }
    }
}
