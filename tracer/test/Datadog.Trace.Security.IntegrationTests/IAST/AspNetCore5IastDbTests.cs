// <copyright file="AspNetCore5IastDbTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_0_OR_GREATER

using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Security.IntegrationTests.Iast;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.IAST;

[Trait("RequiresDockerDependency", "true")]
public class AspNetCore5IastDbTests : AspNetCore5IastTests
{
    public AspNetCore5IastDbTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper, enableIast: true, testName: "AspNetCore5IastDbTestsIastEnabled", samplingRate: 100, vulnerabilitiesPerRequest: 200, isIastDeduplicationEnabled: false, sampleName: "AspNetCore5")
    {
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [InlineData("System.Data.SQLite")]
    [InlineData("Microsoft.Data.Sqlite")]
    [InlineData("System.Data.SqlClient")]
    [InlineData("Npgsql")]
    [InlineData("MySql.Data")]
    // [InlineData("Oracle")] // This test requires the Oracle DB image, which is huge (8GB unpacked), so we cannot enable it without taking precautionary measures.
    public async Task TestIastStoredXssRequest(string database)
    {
#if NETCOREAPP3_0
        if (database == "Microsoft.Data.Sqlite" && EnvironmentHelper.IsAlpine())
        {
            throw new SkipException();
        }
#endif
        var filename = "Iast.StoredXss.AspNetCore5.IastEnabled";
        if (RedactionEnabled is true) { filename += ".RedactionEnabled"; }
        var url = $"/Iast/StoredXss?param=<b>RawValue</b>&database={database}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        settings.AddRegexScrubber(aspNetCorePathScrubber);
        settings.AddRegexScrubber(hashScrubber);
        settings.AddRegexScrubber((new Regex(@"&database=.*"), "&database=...,"));
        // Oracle column names are all upper case
        settings.AddRegexScrubber((new Regex(@"\""DETAILS\"""), @"""Details"""));
        // Postgres column names are all lower case
        settings.AddRegexScrubber((new Regex(@"\""details\"""), @"""Details"""));

        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [InlineData("System.Data.SQLite")]
    [InlineData("Microsoft.Data.Sqlite")]
    [InlineData("System.Data.SqlClient")]
    [InlineData("Npgsql")]
    [InlineData("MySql.Data")]
    // [InlineData("Oracle")] // This test requires the Oracle DB image, which is huge (8GB unpacked), so we cannot enable it without taking precautionary measures.
    public async Task TestIastStoredXssEscapedRequest(string database)
    {
#if NETCOREAPP3_0
        if (database == "Microsoft.Data.Sqlite" && EnvironmentHelper.IsAlpine())
        {
            throw new SkipException();
        }
#endif

        var filename = "Iast.StoredXssEscaped.AspNetCore5.IastEnabled";
        var url = $"/Iast/StoredXssEscaped?database={database}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();

        // Add a scrubber to remove the useMicrosoftDataDb value
        settings.AddRegexScrubber((new Regex(@"database=.*"), "database=...,"));

        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }

    [SkippableTheory]
    [Trait("Category", "ArmUnsupported")]
    [InlineData("System.Data.SQLite")]
    [InlineData("Microsoft.Data.Sqlite")]
    [InlineData("System.Data.SqlClient")]
    [InlineData("Npgsql")]
    [InlineData("MySql.Data")]
    // [InlineData("Oracle")] // This test requires the Oracle DB image, which is huge (8GB unpacked), so we cannot enable it without taking precautionary measures.
    public async Task TestIastStoredSqliRequest(string database)
    {
#if NETCOREAPP3_0
        if (database == "Microsoft.Data.Sqlite" && EnvironmentHelper.IsAlpine())
        {
            throw new SkipException();
        }
#endif

        var filename = "Iast.StoredSqli.AspNetCore5.IastEnabled";
        var url = $"/Iast/StoredSqli?database={database}";
        IncludeAllHttpSpans = true;
        await TryStartApp();
        var agent = Fixture.Agent;
        var spans = await SendRequestsAsync(agent, 2, new string[] { url });
        var spansFiltered = spans.Where(x => x.Type == SpanTypes.Web || x.Type == SpanTypes.IastVulnerability).ToImmutableList();

        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddIastScrubbing();
        settings.AddRegexScrubber(aspNetCorePathScrubber);
        settings.AddRegexScrubber(hashScrubber);
        // Add a scrubber to remove the useMicrosoftDataDb value
        settings.AddRegexScrubber((new Regex(@"database=.*"), "database=...,"));
        // Oracle column names are all upper case
        settings.AddRegexScrubber((new Regex(@"\""DETAILS\"""), @"""Details"""));
        // Postgres column names are all lower case
        settings.AddRegexScrubber((new Regex(@"\""details\"""), @"""Details"""));

        await VerifySpans(spansFiltered, settings, fileNameOverride: filename);
    }
}

#endif
