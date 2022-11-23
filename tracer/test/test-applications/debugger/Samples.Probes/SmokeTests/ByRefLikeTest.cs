using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(18)]
    [LineProbeTestData(19)]
    [LineProbeTestData(20)]
    internal class ByRefLikeTest : IRun
    {
        public void Run()
        {
            var byRefLike = new ByRefLike(nameof(ByRefLikeTest));
            byRefLike.CallMe("Hello from the outside 1!", byRefLike, ref byRefLike);
            byRefLike.CallMe2("Hello from the outside 2!", byRefLike, ref byRefLike);
            byRefLike.CallMe3("Hello from the outside 3!", byRefLike, ref byRefLike);
        }

        ref struct ByRefLike
        {
            private string _whoAmI;

            public ByRefLike(string whoAmI)
            {
                _whoAmI = whoAmI;
            }

            [MethodProbeTestData]
            public ref ByRefLike CallMe(string @in, ByRefLike byRefLike, ref ByRefLike refByRefLike)
            {
                return ref refByRefLike;
            }

            [MethodProbeTestData]
            public ByRefLike CallMe2(string @in, ByRefLike byRefLike, ref ByRefLike refByRefLike)
            {
                return byRefLike;
            }

            [MethodProbeTestData]
            public string CallMe3(string @in, ByRefLike byRefLike, ref ByRefLike refByRefLike)
            {
                return @in + "Hello World";
            }
        }
    }
}
