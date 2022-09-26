using System;
using System.Runtime.CompilerServices;

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
