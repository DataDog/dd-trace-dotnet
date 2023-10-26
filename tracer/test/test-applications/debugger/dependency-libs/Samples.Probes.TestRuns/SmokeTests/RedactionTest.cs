using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class RedactionTest : IRun
    {
        [LogMethodProbeTestData]
        public void Run()
        {
            var _Pass_WoRD_ = "PWPW";
            Echo(_Pass_WoRD_, _Pass_WoRD_, _Pass_WoRD_);
            var _SeC_ReT = "Secret";
            Echo(_SeC_ReT, _Pass_WoRD_, _SeC_ReT);
            var a = new OuterClass();
            Echo(a, _Pass_WoRD_, _SeC_ReT);
            var b = Echo(a, _Pass_WoRD_, _SeC_ReT);
            Echo(b, _Pass_WoRD_, _SeC_ReT);
        }

        public T Echo<T>(T a, string str1, string str2)
        {
            return a;
        }
    }

    class OuterClass
    {
        public string Id { get; set; }
        public string PassWord { get; set; }
        public string RedactMe { get; set; }
    }
}
