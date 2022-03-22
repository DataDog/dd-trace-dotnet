using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    internal class OpenGenericMethodInDerivedGenericType : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            new Test2<OpenGenericMethodInDerivedGenericType>().Method(new Generic(), new OpenGenericMethodInDerivedGenericType(), new Generic());
        }

        public class Test2<Generic2> : HasVarAndMvar.Test<Generic>
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.String", new[] { "!!0", "!0", "Samples.Probes.Shared.Generic" })]
            public string Method<K>(K k, Generic2 gen2, Generic gen)
            {
                var kToString = k.ToString();
                var gen2ToString = gen2.ToString();
                var genToString = gen.ToString();
                return kToString + gen2ToString + genToString;
            }
        }
    }
}
