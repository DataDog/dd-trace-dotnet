// <copyright file="AspNetCore5ProcessorOverrides.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Google.Protobuf.Compiler.CodeGeneratorResponse.Types;

namespace Datadog.Trace.Security.IntegrationTests;

public class AspNetCore5ProcessorOverrides : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    private const string Pattern = "CUSTOM_TEST_PATTERN_12345";
    private const string RuleId = "custom-scanner-detection-rule";
    private const string CustomRulesPath = "processor-overrides.json";

    private readonly AspNetCoreTestFixture _fixture;

    public AspNetCore5ProcessorOverrides(AspNetCoreTestFixture fixture, ITestOutputHelper output)
        : base("AspNetCore5", output, "/shutdown", testName: nameof(AspNetCore5ProcessorOverrides), allowAutoRedirect: false)
    {
        _fixture = fixture;
        _fixture.SetOutput(output);
    }

    public override void Dispose()
    {
        base.Dispose();
        _fixture.SetOutput(null);
    }

    public async Task TryStartApp()
    {
        await _fixture.TryStartApp(this, enableSecurity: true, externalRulesFile: CustomRulesPath);
        SetHttpPort(_fixture.HttpPort);
    }

    [SkippableTheory]
    [InlineData("triggers_rule", $"property={Pattern}")]
    [InlineData("no_trigger", $"property=normal_text_without_pattern")]
    public async Task CustomScanner_WithProcessorOverrides(string testCase, string body)
    {
        await TryStartApp();
        var agent = _fixture.Agent;
        var url = "/datarazorpage";
        // var body = $"property={Pattern}";

        var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
        var settings = VerifyHelper.GetSpanVerifierSettings(testCase, "-");
        var contentType = "application/x-www-form-urlencoded";
        if (url.Contains("api"))
        {
            contentType = "application/json";
        }

        await TestAppSecRequestWithVerifyAsync(agent, url, body, 5, 1, settings, contentType);
    }
}
#endif
