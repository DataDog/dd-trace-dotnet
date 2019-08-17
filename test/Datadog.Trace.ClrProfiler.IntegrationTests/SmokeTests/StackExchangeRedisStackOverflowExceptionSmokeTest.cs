using System.Collections.Generic;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class StackExchangeRedisStackOverflowExceptionSmokeTest : SmokeTestBase
    {
        public StackExchangeRedisStackOverflowExceptionSmokeTest(ITestOutputHelper output)
            : base(output, "StackExchange.Redis.StackOverflowException", maxTestRunSeconds: 15)
        {
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            if (EnvironmentHelper.IsWindows())
            {
                Output.WriteLine("Ignored for Windows");
                return;
            }

            CheckForSmoke();
        }

        protected override void AssertProcessResultIsSuccessful(ProcessResult result)
        {
            var successCodes = new HashSet<int> { 0, 139 };
            Assert.True(successCodes.Contains(result.ExitCode), $"Non-success exit code {result.ExitCode}. Expected exit codes: {string.Join(",", successCodes)}");
            Assert.True(string.IsNullOrEmpty(result.StandardError), $"Expected no errors in smoke test: {result.StandardError}");
        }
    }
}
