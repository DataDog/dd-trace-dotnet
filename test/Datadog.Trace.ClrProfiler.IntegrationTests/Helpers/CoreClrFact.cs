using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public class CoreClrFact : FactAttribute
    {
        public CoreClrFact()
        {
            if (!EnvironmentHelper.IsCoreClr())
            {
                Skip = "Ignore for .NET Framework";
            }
        }
    }
}
