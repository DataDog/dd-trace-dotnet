using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    internal class HasVarAndMvar<T> : IRun where T : IGeneric
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method(new Generic());
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        // https://datadoghq.atlassian.net/browse/DEBUG-722
        [MethodProbeTestData("System.Collections.Generic.List`1<Samples.Probes.SmokeTests`1<!0>>", new[] { "!!0" }, true)]
        public List<HasVarAndMvar<T>> Method<K>(K k) where K : IGeneric
        {
            var @string = k.ToString();
            System.Console.WriteLine(@string);
            var kk = new List<HasVarAndMvar<K>>() { new HasVarAndMvar<K>() };
            var tt = kk.Select(item => new HasVarAndMvar<T>());
            return tt.ToList();
        }
    }
}
