// <copyright file="SqlInjectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[UsesVerify]
public class SqlInjectionTests : TestHelper
{
    private const string ExpectedOperationName = "sql_injection";
    private static readonly Regex LocationMsgRegex = new(@"(\S)*""location"": {(\r|\n){1,2}(.*(\r|\n){1,2}){0,3}(\s)*},");
    private static readonly Regex HashRegex = new(@"(\S)*""hash"": (-){0,1}([0-9]){1,12},(\r|\n){1,2}      ");

    public SqlInjectionTests(ITestOutputHelper output)
        : base("SQLite.Core", output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_ENABLED", "true");

        const int expectedSpanCount = 6;
        var filename = "SQlInjection.SubmitsTraces";
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = RunSampleAndWaitForExit(agent);
        var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddRegexScrubber(LocationMsgRegex, string.Empty);
        settings.AddRegexScrubber(HashRegex, string.Empty);
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

        VerifyInstrumentation(process.Process);
    }
}
