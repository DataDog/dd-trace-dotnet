using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class SpanOnMethodProbeTestDataAttribute : MethodProbeTestDataAttribute
    {
    }
}
