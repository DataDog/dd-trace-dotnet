using System;

namespace Samples.Probes.TestRuns
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class SpanOnMethodProbeTestDataAttribute : MethodProbeTestDataAttribute
    {
        public SpanOnMethodProbeTestDataAttribute(bool skip = false) : 
            base(skip: skip)
        {

        }
    }
}
