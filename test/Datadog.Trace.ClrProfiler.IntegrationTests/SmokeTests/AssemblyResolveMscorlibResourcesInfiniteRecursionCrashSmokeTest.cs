using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class AssemblyResolveMscorlibResourcesInfiniteRecursionCrashSmokeTest : SmokeTestBase
    {
        public AssemblyResolveMscorlibResourcesInfiniteRecursionCrashSmokeTest(ITestOutputHelper output)
            : base(output, "AssemblyResolveMscorlibResources.InfiniteRecursionCrash")
        {
        }

        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke();
        }
    }
}
