using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class HasFieldLazyValueInitialized : IRun
    {
        public Lazy<string> FirstName = new Lazy<string>(new Func<string>(() => "First"));

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(FirstName.Value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.String", new[] { "System.String" })]
        public string Method(string lastName)
        {
            return lastName;
        }
    }
}
