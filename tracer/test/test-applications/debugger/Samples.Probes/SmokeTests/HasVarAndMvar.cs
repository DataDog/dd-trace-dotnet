using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    internal class HasVarAndMvar : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            new Test<Generic>().Method(new Generic());
        }

        public class Test<T> where T : IGeneric
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.Collections.Generic.List`1<Samples.Probes.SmokeTests`1<!0>>", new[] { "!!0" })]
            public List<Test<T>> Method<K>(K k) where K : IGeneric
            {
                var @string = k.ToString();
                System.Console.WriteLine(@string);
                var kk = new List<Test<K>>() { new Test<K>() };
                System.Console.WriteLine(kk);
                var tt = new List<Test<T>>() { new Test<T>() };
                return tt;
            }
        }
    }
}
