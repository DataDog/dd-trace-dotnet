using System.Collections.Generic;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class ObscenelyAnnoyingClass
    {
        public MethodCallMetadata LastCall { get; private set; }

        public Dictionary<int, List<MethodCallMetadata>> CallCountsPerMetadataToken { get; } = new Dictionary<int, List<MethodCallMetadata>>();

        public void Method()
        {
            SetLastCall(MethodBase.GetCurrentMethod());
        }

        public void Method(int i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i);
        }

        public void Method(object i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i);
        }

        public void Method(string i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i);
        }

        public void Method(long i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i);
        }

        public void Method(short i)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), i);
        }

        protected void SetLastCall(MethodBase currentMethod, params object[] wholeBunchOfGarbage)
        {
            if (CallCountsPerMetadataToken.ContainsKey(currentMethod.MetadataToken) == false)
            {
                CallCountsPerMetadataToken.Add(currentMethod.MetadataToken, new List<MethodCallMetadata>());
            }

            LastCall = new MethodCallMetadata { MetadataToken = currentMethod.MetadataToken, Parameters = wholeBunchOfGarbage };
            CallCountsPerMetadataToken[currentMethod.MetadataToken].Add(LastCall);
        }
    }
}
