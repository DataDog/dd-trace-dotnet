using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    internal class AsyncMethodRedactionTest : IAsyncRun
    {
        [LogMethodProbeTestData]
        public async Task RunAsync()
        {
            await Task.Yield();
            var _Pass_WoRD_ = "PWPW";
            await Task.Yield();
            Echo(_Pass_WoRD_, _Pass_WoRD_, _Pass_WoRD_);
            await Task.Yield();
            var _SeC_ReT = "Secret";
            Echo(_SeC_ReT, _Pass_WoRD_, _SeC_ReT);
            await Task.Yield();
            var a = new OuterClass();
            await Task.Yield();
            Echo(a, _Pass_WoRD_, _SeC_ReT);
            await Task.Yield();
            var b = Echo(a, _Pass_WoRD_, _SeC_ReT);
            await Task.Yield();
            Echo(b, _Pass_WoRD_, _SeC_ReT);
            await Task.Yield();
        }

        public T Echo<T>(T a, string str1, string str2)
        {
            return a;
        }
    }
}
