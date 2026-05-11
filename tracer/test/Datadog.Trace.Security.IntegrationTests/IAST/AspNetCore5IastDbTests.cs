// <copyright file="AspNetCore5IastDbTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Security.IntegrationTests.Iast;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

[Trait("RequiresDockerDependency", "true")]
public class AspNetCore5IastDbTests : AspNetCore5IastTests
{
    private static readonly Regex DatabaseParamScrubber = new(@"([?&]database=)[^&]+", RegexOptions.Compiled);

    public AspNetCore5IastDbTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, testName: "AspNetCore5IastDbTestsIastEnabled", samplingRate: 100, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, sampleName: "AspNetCore5")
    {
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [InlineData("Microsoft.Data.Sqlite")]
    public async Task TestIastStoredXssRequest(string database)
    {
#if NETCOREAPP3_0
        if (database == "Microsoft.Data.Sqlite" && EnvironmentHelper.IsAlpine())
        {
            throw new SkipException();
        }
#endif
#if NET6_0_OR_GREATER
        var filename = "Iast.StoredXss.AspNetCore5";
#else
        var filename = "Iast.StoredXss.AspNetCore5.v1";
#endif
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = $"/Iast/StoredXss?param=<b>RawValue</b>&database={database}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 2, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since, recordSanitizer: ScrubDatabaseParam);
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [InlineData("System.Data.SQLite")]
    [InlineData("Microsoft.Data.Sqlite")]
    public async Task TestIastStoredXssEscapedRequest(string database)
    {
#if NETCOREAPP3_0
        if (database == "Microsoft.Data.Sqlite" && EnvironmentHelper.IsAlpine())
        {
            throw new SkipException();
        }
#endif
        var url = $"/Iast/StoredXssEscaped?database={database}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 2, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, NotVulnerableSnapshotName, since: since, timeoutMs: 1_000);
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [InlineData("Microsoft.Data.Sqlite")]
    public async Task TestIastStoredSqliRequest(string database)
    {
#if NETCOREAPP3_0
        if (database == "Microsoft.Data.Sqlite" && EnvironmentHelper.IsAlpine())
        {
            throw new SkipException();
        }
#endif

        var filename = "Iast.StoredSqli.AspNetCore5";
        var url = $"/Iast/StoredSqli?database={database}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var since = DateTime.UtcNow;
        await SendRequestsAsync(Fixture.Agent, 2, new[] { url });

        await VerifyVulnerabilityRecordsAsync(VulnerabilityLogPath, filename, since: since, recordSanitizer: ScrubDatabaseParam);
    }

    private void ScrubDatabaseParam(JObject record)
    {
        if (record["request"] is JObject req && req["url"] is JValue urlVal)
        {
            req["url"] = DatabaseParamScrubber.Replace(urlVal.Value<string>() ?? string.Empty, "$1...");
        }
    }
}

#endif
