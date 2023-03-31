using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class OpenGenericMethodInDerivedGenericType : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            new Test2<OpenGenericMethodInDerivedGenericType>().Method(new Generic(), new OpenGenericMethodInDerivedGenericType(), new Generic());
        }

        public class Test2<Generic2> : HasVarAndMvar.Test<Generic>
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            [LogMethodProbeTestData("System.String", new[] { "!!0", "!0", "Samples.Probes.TestRuns.Shared.Generic" })]
            public string Method<K>(K k, Generic2 gen2, Generic gen)
            {
                var kToString = k.ToString();
                var gen2ToString = gen2.ToString();
                var genToString = gen.ToString();
                if (kToString.Length + gen2ToString.Length + genToString.Length > 5)
                {
                    return kToString + gen2ToString + genToString;
                }
                else
                {
                    return genToString + gen2ToString + kToString;
                }
            }
        }
    }
}
