using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class HasFieldLazyValueInitialized : IRun
    {
        public Lazy<string> FirstName = new Lazy<string>(new Func<string>(() => "First"));

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method(FirstName.Value);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "System.String" })]
        public string Method(string lastName)
        {
            return lastName;
        }
    }
}
