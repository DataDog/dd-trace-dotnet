// <copyright file="WeakCipherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[UsesVerify]
public class WeakCipherTests : TestHelper
{
    private const string ExpectedOperationName = "weak_cipher";

    public WeakCipherTests(ITestOutputHelper output)
        : base("WeakCipher", output)
    {
        SetServiceVersion("1.0.0");
    }

#if !NET7_0
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_ENABLED", "true");

        const int expectedSpanCount = 6;
        var filename = "WeakCipherTests.SubmitsTraces";
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = RunSampleAndWaitForExit(agent);
        var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

        VerifyInstrumentation(process.Process);
    }
#endif

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData("DD_IAST_ENABLED", "false")]
    [InlineData("DD_IAST_WEAK_CIPHER_ALGORITHMS", "")]
    [InlineData($"DD_TRACE_{nameof(IntegrationId.SymmetricAlgorithm)}_ENABLED", "false")]
    public void IntegrationDisabled(string variableName, string variableValue)
    {
        SetEnvironmentVariable("DD_IAST_ENABLED", "true");
        SetEnvironmentVariable(variableName, variableValue);
        const int expectedSpanCount = 6;
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = RunSampleAndWaitForExit(agent);
        var spans = agent.WaitForSpans(expectedSpanCount, returnAllOperations: true);

        Assert.Empty(spans.Where(s => s.Name.Equals(ExpectedOperationName)));
    }
}
#endif
