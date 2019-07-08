using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ObscenelyAnnoyingGenericClass<Tc1> : ObscenelyAnnoyingClass
    {
        public void Method(Tc1 i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i);
        }

        public void Method<Tm1>(Tc1 i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i, default(Tm1));
        }
    }
}
