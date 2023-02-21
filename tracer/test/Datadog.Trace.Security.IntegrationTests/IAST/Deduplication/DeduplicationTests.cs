// <copyright file="DeduplicationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

#if NET7_0_OR_GREATER
    [SkippableTheory(Skip = "Flaky in .NET 7")]
#else
    [SkippableTheory]
#endif
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitsTraces(bool deduplicationEnabled)
    {
        SetEnvironmentVariable("DD_TRACE_DEBUG", "1");
        SetEnvironmentVariable("DD_IAST_ENABLED", "1");
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", deduplicationEnabled.ToString());
        SetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", Path.Combine(EnvironmentHelper.LogDirectory));

        int expectedSpanCount = deduplicationEnabled ? 1 : 5;
        var filename = deduplicationEnabled ? "iast.deduplication.deduplicated" : "iast.deduplication.duplicated";

        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = RunSampleAndWaitForExit(agent, "5");
        var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

        VerifyInstrumentation(process.Process);
    }
}
