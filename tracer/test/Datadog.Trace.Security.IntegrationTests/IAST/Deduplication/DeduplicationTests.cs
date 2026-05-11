// <copyright file="DeduplicationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.IntegrationTests.Iast;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[UsesVerify]
public class DeduplicationTests : TestHelper
{
    private readonly string _vulnerabilityLogPath;

    public DeduplicationTests(ITestOutputHelper output)
        : base("Deduplication", output)
    {
        _vulnerabilityLogPath = Path.Combine(LogDirectory, $"iast-vulns-{GetType().Name}.jsonl");
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, _vulnerabilityLogPath);
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
        }

        SetEnvironmentVariable("DD_IAST_ENABLED", "1");
        SetEnvironmentVariable("DD_IAST_DEDUPLICATION_ENABLED", deduplicationEnabled.ToString());
        SetEnvironmentVariable("DD_APPSEC_STACK_TRACE_ENABLED", "false");

        var filename = deduplicationEnabled ? "iast.deduplication.deduplicated" : "iast.deduplication.duplicated";
        if (!onlyWeakHash)
        {
            filename += ".All";
        }

        var since = DateTime.UtcNow;
        using var process = await RunSampleAndWaitForExit("5");

        var allRecords = VulnerabilityJsonl.ReadRecords(_vulnerabilityLogPath, since);

        // Mirror the original operationName filter: when onlyWeakHash=true the old test
        // waited only for "weak_hashing" spans, ignoring WEAK_RANDOMNESS and others.
        var records = onlyWeakHash
            ? allRecords.Where(r => r["type"]?.Value<string>() == "WEAK_HASH").ToList()
            : allRecords;

        if (!instrumented)
        {
            Assert.Empty(records);
            return;
        }

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
}
