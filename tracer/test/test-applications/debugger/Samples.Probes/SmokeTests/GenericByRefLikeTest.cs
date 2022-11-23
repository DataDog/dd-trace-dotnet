using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(26)]
    [LineProbeTestData(27)]
    [LineProbeTestData(28)]
    [LineProbeTestData(29)]
    [LineProbeTestData(30)]
    [LineProbeTestData(32)]
    [LineProbeTestData(33)]
    [LineProbeTestData(34)]
    [LineProbeTestData(35)]
    [LineProbeTestData(37)]
    [LineProbeTestData(38)]
    [LineProbeTestData(53)]
    public class GenericByRefLikeTest : IRun
    {
        public void Run()
        {
            var place = new Place { @Type = PlaceType.City, Name = "New York" };
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

            [MethodProbeTestData]
            public ref GenericByRefLike<T> CallMe(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return ref refByRefLike;
            }

            [MethodProbeTestData]
            public GenericByRefLike<T> CallMe2(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return byRefLike;
            }

            [MethodProbeTestData]
            public string CallMe3(string @in, GenericByRefLike<T> byRefLike, ref GenericByRefLike<T> refByRefLike)
            {
                return @in + "Hello World";
            }

            [MethodProbeTestData]
            public string CallMe4<K>(string @in, GenericByRefLike<K> byRefLike, ref GenericByRefLike<K> refByRefLike)
            {
                return @in + "Hello World";
            }
        }
    }
}
