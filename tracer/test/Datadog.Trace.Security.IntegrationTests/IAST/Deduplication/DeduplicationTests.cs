// <copyright file="DeduplicationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[UsesVerify]
public class DeduplicationTests : TestHelper
{
    private const string ExpectedOperationName = "weak_hashing";
    private static readonly Regex LocationMsgRegex = new(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,3}(\s)*},");

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
        SetEnvironmentVariable("DD_IAST_ENABLED", "true");
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", deduplicationEnabled.ToString());

        int expectedSpanCount = deduplicationEnabled ? 1 : 5;
        var filename = deduplicationEnabled ? "iast.deduplication.deduplicated" : "iast.deduplication.duplicated";

        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = RunSampleAndWaitForExit(agent, "5");
        var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddRegexScrubber(LocationMsgRegex, string.Empty);
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

        VerifyInstrumentation(process.Process);
    }
}
#endif
