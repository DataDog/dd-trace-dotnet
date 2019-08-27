using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class EntityFramework6xMdTokenLookupFailure : SmokeTestBase
    {
        public EntityFramework6xMdTokenLookupFailure(ITestOutputHelper output)
            : base(output, "EntityFramework6x.MdTokenLookupFailure", maxTestRunSeconds: 60)
        {
        }

        [TargetFrameworkVersionsFact("net452")]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
