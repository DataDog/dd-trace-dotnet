// <copyright file="DeduplicationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[UsesVerify]
public class DeduplicationTests : TestHelper
{
    private const string ExpectedOperationName = "weak_hashing";

    public DeduplicationTests(ITestOutputHelper output)
        : base("Deduplication", output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(true, "", "", false)]
    [InlineData(false, "DD_IAST_WEAK_HASH_ALGORITHMS", "noexistingalgorithm")]
    [InlineData(false, $"DD_TRACE_{nameof(IntegrationId.HashAlgorithm)}_ENABLED", "false")]
    public async Task SubmitsTraces(bool deduplicationEnabled, string disableKey = "", string disableValue = "", bool onlyWeakHash = true)
    {
        bool instrumented = string.IsNullOrEmpty(disableKey);
        if (!instrumented)
        {
            SetEnvironmentVariable(disableKey, disableValue);
            instrumented = false;
        }

        SetEnvironmentVariable("DD_IAST_ENABLED", "1");
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", deduplicationEnabled.ToString());
        SetEnvironmentVariable("DD_APPSEC_STACK_TRACE_ENABLED", "false");

        int expectedSpanCount = instrumented ? (deduplicationEnabled ? 2 : 10) : 0;
        var filename = deduplicationEnabled ? "iast.deduplication.deduplicated" : "iast.deduplication.duplicated";

        if (!onlyWeakHash)
        {
            filename += ".All";
        }

        using var agent = EnvironmentHelper.GetMockAgent();
        // Disable meta struct for this test because if the sample run faster than the service discovery,
        // the config from the mock agent is not fetched
        agent.Configuration.SpanMetaStructs = false;

        using var process = await RunSampleAndWaitForExit(agent, "5");
        var spans = await (
            onlyWeakHash ?
                agent.WaitForSpansAsync(expectedSpanCount, operationName: ExpectedOperationName) :
                agent.WaitForSpansAsync(expectedSpanCount));

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();

        if (instrumented)
        {
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName(filename)
                              .DisableRequireUniquePrefix();

            VerifyInstrumentation(process.Process);
        }
        else
        {
            Assert.Empty(spans);
        }
    }
}
