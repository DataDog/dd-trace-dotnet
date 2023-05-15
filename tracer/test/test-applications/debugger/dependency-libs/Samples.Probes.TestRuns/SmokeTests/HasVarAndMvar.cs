using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class HasVarAndMvar : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            new Test<Generic>().Method(new Generic());
        }

        public class Test<T> where T : IGeneric
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            [LogMethodProbeTestData("System.Collections.Generic.List`1<Samples.Probes.TestRuns.SmokeTests`1<!0>>", new[] { "!!0" })]
            public List<Test<T>> Method<K>(K k) where K : IGeneric
            {
                var @string = k.ToString();
                if (@string?.Length > 1)
                {
                    System.Console.WriteLine(@string);
                }
                else
                {
                    Console.WriteLine(k.Message);
                }

                var kk = new List<Test<K>>() { new Test<K>() };
                var tt = new List<Test<T>>() { new Test<T>() };

                if (kk.First().Method(DateTime.Now.Second))
                {
                    Console.WriteLine(kk);
                }
                else
                {
                    Console.WriteLine(tt);
                }

                return tt;
            }

            bool Method(int n)
            {
                return n % 2 == 0;
            }
        }
    }
}
