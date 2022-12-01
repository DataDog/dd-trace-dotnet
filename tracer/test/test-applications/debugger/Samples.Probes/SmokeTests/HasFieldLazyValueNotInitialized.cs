using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class HasFieldLazyValueNotInitialized : IRun
    {
        public Lazy<string> FirstName = new Lazy<string>(new Func<string>(() => "First"));

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("Last");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData("System.String", new[] { "System.String" })]
        public string Method(string lastName)
        {
            return lastName;
        }
    }
}
