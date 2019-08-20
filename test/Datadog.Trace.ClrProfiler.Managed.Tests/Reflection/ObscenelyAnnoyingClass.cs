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

        public void Method(ClassB p1, ClassC p2, ClassC p3)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), p1, p2, p3);
        }

        public void Method(object p1, object p2, object p3)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), p1, p2, p3);
        }

        public void Method(ClassA p1, ClassA p2, ClassA p3)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), p1, p2, p3);
        }

        public void Method(ClassA p1, ClassB p2, ClassB p3)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), p1, p2, p3);
        }

        public void Method(AbstractAlphabetClass p1, AbstractAlphabetClass p2, AbstractAlphabetClass p3)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), p1, p2, p3);
        }

        public void Method(ClassB p1, ClassB p2, ClassB p3)
        {
            SetLastCall(MethodBase.GetCurrentMethod(), p1, p2, p3);
        }

        // There is intentionally no method matching double ClassC signature to force non-explicit fuzzy matching
        // public void Method(ClassC p1, ClassC p2)

        protected void SetLastCall(MethodBase currentMethod, params object[] wholeBunchOfGarbage)
        {
            if (CallCountsPerMetadataToken.ContainsKey(currentMethod.MetadataToken) == false)
            {
                CallCountsPerMetadataToken.Add(currentMethod.MetadataToken, new List<MethodCallMetadata>());
            }

            LastCall = new MethodCallMetadata { MethodString = currentMethod.ToString(), MetadataToken = currentMethod.MetadataToken, Parameters = wholeBunchOfGarbage };
            CallCountsPerMetadataToken[currentMethod.MetadataToken].Add(LastCall);
        }
    }
}
