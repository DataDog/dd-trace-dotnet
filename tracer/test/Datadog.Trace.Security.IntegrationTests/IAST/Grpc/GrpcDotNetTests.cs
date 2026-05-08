// <copyright file="GrpcDotNetTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.IAST.GrpcDotNet;

[UsesVerify]
public class GrpcDotNetTests : TestHelper
{
    private readonly string _vulnerabilityLogPath;

    public GrpcDotNetTests(ITestOutputHelper output)
        : base("GrpcDotNet", output)
    {
        _vulnerabilityLogPath = Path.Combine(LogDirectory, $"iast-vulns-{GetType().Name}.jsonl");

        SetServiceVersion("1.0.0");
        SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        SetEnvironmentVariable(ConfigurationKeys.Iast.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RedactionEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilitiesPerRequest, "200");
        SetEnvironmentVariable(ConfigurationKeys.Iast.RequestSampling, "100");
        SetEnvironmentVariable(ConfigurationKeys.Iast.MaxConcurrentRequests, "100");
        SetEnvironmentVariable(ConfigurationKeys.Iast.IsIastDeduplicationEnabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.Iast.VulnerabilityLogPath, _vulnerabilityLogPath);

        SetEnvironmentVariable("IAST_GRPC_SOURCE_TEST", "1");
        SetEnvironmentVariable("DD_APPSEC_STACK_TRACE_ENABLED", "false");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task SubmitsTraces()
    {
        GuardAlpine();
        SkipOn.Platform(SkipOn.PlatformValue.Linux);

        const string filename = "Iast.GrpcDotNetTests.BodyPropagation.SubmitsTraces";

        using var agent = EnvironmentHelper.GetMockAgent();
        var since = DateTime.UtcNow;
        using var process = await RunSampleAndWaitForExit(agent);

        // Process has exited — the JSONL file is fully written. Read only records from this run.
        var records = ReadAllVulnerabilityRecords(_vulnerabilityLogPath, since);
        var sanitized = records
            .OrderBy(r => r["type"]?.Value<string>(), StringComparer.Ordinal)
            .ThenBy(r => r["hash"]?.Value<int>())
            .Select(Sanitize)
            .ToList();

        VerifyHelper.InitializeGlobalSettings();
        await Verifier.Verify(sanitized, new VerifySettings())
                      .UseFileName(filename)
                      .DisableRequireUniquePrefix();
    }

    private static List<JObject> ReadAllVulnerabilityRecords(string path, DateTime since)
    {
        var records = new List<JObject>();
        if (!File.Exists(path))
        {
            return records;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JObject record;
            try
            {
                record = JObject.Parse(line);
            }
            catch
            {
                continue;
            }

            var ts = record["timestamp"]?.Value<DateTime?>();
            if (ts is null || ts.Value < since)
            {
                continue;
            }

            records.Add(record);
        }

        return records;
    }

    private static JObject Sanitize(JObject record)
    {
        record.Remove("timestamp");

        if (record["location"] is JObject location)
        {
            location.Remove("line");
            location.Remove("stack");
        }

        return record;
    }

    private static void GuardAlpine()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine")))
        {
            throw new SkipException("GRPC.Tools does not support Alpine");
        }
    }
}
#endif
