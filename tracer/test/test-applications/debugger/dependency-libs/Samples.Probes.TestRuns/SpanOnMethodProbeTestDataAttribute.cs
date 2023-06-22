using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = true)]
    public class SpanOnMethodProbeTestDataAttribute : MethodProbeTestDataAttribute
    {
        public SpanOnMethodProbeTestDataAttribute(bool skip = false, int phase = 1) : 
            base(skip: skip, phase: phase)
        {

        }
    }
}
