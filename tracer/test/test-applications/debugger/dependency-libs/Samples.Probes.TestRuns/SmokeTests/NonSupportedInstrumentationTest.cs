using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests;

public class NonSupportedInstrumentationTest : IRun
{
    public void Run()
    {
        new GenericStructIsNotSupported<int>().MethodToInstrument(nameof(Run));
    }

    struct GenericStructIsNotSupported<T>
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)] // Should fail on instrumentation as we don't support the instrumentation of methods that reside inside an inner generic struct.
        public void MethodToInstrument(string callerName)
        {
            var arr = new[] { callerName, nameof(MethodToInstrument), nameof(SimpleTypeNameTest) };
            if (NoOp(arr).Length == arr.Length)
            {
                throw new IntentionalDebuggerException("Same length.");
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        string[] NoOp(string[] arr) => arr;
    }
}
