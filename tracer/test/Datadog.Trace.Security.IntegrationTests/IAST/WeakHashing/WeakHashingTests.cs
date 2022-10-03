// <copyright file="WeakHashingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast
{
    [UsesVerify]
    public class WeakHashingTests : TestHelper
    {
        private const string ExpectedOperationName = "weak_hashing";
        private static readonly Regex PathMsgRegex = new(@"(\S)*""Path"": "".*"",(\r|\n){1,2}");

        public WeakHashingTests(ITestOutputHelper output)
            : base("WeakHashing", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            SetEnvironmentVariable("DD_IAST_ENABLED", "true");

#if NET6_0 || NET5_0
            const int expectedSpanCount = 28;
            var filename = "WeakHashingTestsTests.SubmitsTraces.Net50.60";
#else
            const int expectedSpanCount = 21;
            var filename = "WeakHashingTestsTests.SubmitsTraces";
#endif

            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddRegexScrubber(PathMsgRegex, string.Empty);
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();

            VerifyInstrumentation(process.Process);
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData("DD_IAST_ENABLED", "false")]
        [InlineData("DD_IAST_WEAK_HASH_ALGORITHMS", "")]
        [InlineData($"D_TRACE_{nameof(IntegrationId.HashAlgorithm)}_ENABLED", "false")]
        public void IntegrationDisabled(string variableName, string variableValue)
        {
            SetEnvironmentVariable(variableName, variableValue);
            const int expectedSpanCount = 21;
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent);
            var spans = agent.WaitForSpans(expectedSpanCount, returnAllOperations: true);

            Assert.Empty(spans.Where(s => s.Name.Equals(ExpectedOperationName)));
        }
    }
}
#endif
