// <copyright file="WeakHashingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[UsesVerify]
public class WeakHashingTests : TestHelper
{
    private const string ExpectedOperationName = "weak_hashing";

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
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_TRACE_DEBUG", "true");
        SetEnvironmentVariable("DD_IAST_ENABLED", "true");
        // Avoid tests parallel log collision
        SetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", Path.Combine(EnvironmentHelper.LogDirectory, "WeakHashingLogs"));

#if NET5_0_OR_GREATER
        const int expectedSpanCount = 4 * 8;
        var filename = "WeakHashingTests.SubmitsTraces.Net50.60";
#elif NETCOREAPP
        const int expectedSpanCount = 3 * 8;
        var filename = "WeakHashingTests.SubmitsTraces";
#else
        const int expectedSpanCount = 3 * 9;
        var filename = "WeakHashingTests.SubmitsTraces.Net462";
#endif

        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = RunSampleAndWaitForExit(agent);
        var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);

        using var s = new AssertionScope();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        await VerifyHelper.VerifySpans(spans, settings)
                          .UseFileName(filename)
                          .DisableRequireUniquePrefix();

        VerifyInstrumentation(process.Process);
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData("DD_IAST_ENABLED", "false")]
    [InlineData("DD_IAST_WEAK_HASH_ALGORITHMS", "noexistingalgorithm")]
    [InlineData($"DD_TRACE_{nameof(IntegrationId.HashAlgorithm)}_ENABLED", "false")]
    public void IntegrationDisabled(string variableName, string variableValue)
    {
        SetEnvironmentVariable("DD_IAST_ENABLED", "true");
        SetEnvironmentVariable(variableName, variableValue);
        // Avoid tests parallel log collision
        SetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", Path.Combine(EnvironmentHelper.LogDirectory, "WeakHashingLogs"));

        const int expectedSpanCount = 21;
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = RunSampleAndWaitForExit(agent);
        var spans = agent.WaitForSpans(expectedSpanCount, returnAllOperations: true);

        Assert.Empty(spans.Where(s => s.Name.Equals(ExpectedOperationName)));
    }
}
