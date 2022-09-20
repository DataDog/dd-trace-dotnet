using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Instrumentation;

namespace Samples.Probes.SmokeTests
{
    public class CtorTransparentCodeTest : IRun
    {
        public void Run()
        {
            var instance = new Samples.Probes.External.SecurityTransparentTest();
        }
    }
}
