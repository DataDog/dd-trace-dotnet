// <copyright file="WeakCipherTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[UsesVerify]
public class WeakCipherTests : TestHelper
{
    private readonly string _vulnerabilityLogPath;

    public WeakCipherTests(ITestOutputHelper output)
        : base("WeakCipher", output)
    {
        _vulnerabilityLogPath = Path.Combine(LogDirectory, $"iast-vulns-{GetType().Name}.jsonl");
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable("DD_APPSEC_STACK_TRACE_ENABLED", "false");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, _vulnerabilityLogPath);
    }

#if !NET7_0_OR_GREATER
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", "false");
        SetEnvironmentVariable("DD_IAST_ENABLED", "true");

        const string filename = "WeakCipherTests.SubmitsTraces";

        using var agent = EnvironmentHelper.GetMockAgent();
        var since = DateTime.UtcNow;
        using var process = await RunSampleAndWaitForExit();

        var records = VulnerabilityJsonl.ReadRecords(_vulnerabilityLogPath, since);
        var sanitized = records
            .OrderBy(r => r["type"]?.Value<string>(), StringComparer.Ordinal)
            .ThenBy(r => r["hash"]?.Value<int>())
            .Select(r => VulnerabilityJsonl.Sanitize(r))
            .ToList();

        VerifyHelper.InitializeGlobalSettings();
        await Verifier.Verify(sanitized, new VerifySettings())
                      .UseFileName(filename)
                      .DisableRequireUniquePrefix();

        VerifyInstrumentation(process.Process);
    }
#endif

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData("DD_IAST_ENABLED", "false")]
    [InlineData("DD_IAST_WEAK_CIPHER_ALGORITHMS", "invalidAlgorithm")]
    [InlineData($"DD_TRACE_{nameof(IntegrationId.SymmetricAlgorithm)}_ENABLED", "false")]
    public async Task IntegrationDisabled(string variableName, string variableValue)
    {
        SetEnvironmentVariable("DD_IAST_ENABLED", "true");
        SetEnvironmentVariable(variableName, variableValue);

        using var agent = EnvironmentHelper.GetMockAgent();
        var since = DateTime.UtcNow;
        using var process = await RunSampleAndWaitForExit();

        var records = VulnerabilityJsonl.ReadRecords(_vulnerabilityLogPath, since);
        Assert.Empty(records);
    }
}
