// <copyright file="AspNetCoreNetFrameworkIisMvcTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore;

[Collection("IisTests")]
public class AspNetCoreIisNetFrameworkMvc21Tests(IisFixture fixture, ITestOutputHelper output)
    : AspNetCoreIisNetFrameworkMvcTests("AspNetCoreMvc21", fixture, output);

[Collection("IisTests")]
public class AspNetCoreIisNetFrameworkMvc22Tests(IisFixture fixture, ITestOutputHelper output)
    : AspNetCoreIisNetFrameworkMvcTests("AspNetCoreMvc22", fixture, output);

[Collection("IisTests")]
public class AspNetCoreIisNetFrameworkMvc21DisabledTests(IisFixture fixture, ITestOutputHelper output)
    : AspNetCoreIisNetFrameworkMvcTestsDisabled("AspNetCoreMvc21", fixture, output);

[Collection("IisTests")]
public class AspNetCoreIisNetFrameworkMvc22DisabledTests(IisFixture fixture, ITestOutputHelper output)
    : AspNetCoreIisNetFrameworkMvcTestsDisabled("AspNetCoreMvc22", fixture, output);

[Collection("IisTests")]
public class AspNetCoreIisNetFrameworkMvc21ResourceBasedSamplingTests(IisFixture fixture, ITestOutputHelper output)
    : AspNetCoreIisNetFrameworkMvcResourceBasedSamplingTest("AspNetCoreMvc21", fixture, output);

[Collection("IisTests")]
public class AspNetCoreIisNetFrameworkMvc22ResourceBasedSamplingTests(IisFixture fixture, ITestOutputHelper output)
    : AspNetCoreIisNetFrameworkMvcResourceBasedSamplingTest("AspNetCoreMvc22", fixture, output);

[UsesVerify]
public abstract class AspNetCoreIisNetFrameworkMvcTestsDisabled : AspNetCoreNetFrameworkIisMvcTestsBase, IAsyncLifetime
{
    protected AspNetCoreIisNetFrameworkMvcTestsDisabled(string sampleName, IisFixture fixture, ITestOutputHelper output)
        : base(sampleName, fixture, output)
    {
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, "false");
        // To keep the name the same between samples
        SetServiceName(nameof(AspNetCoreIisNetFrameworkMvcTestsDisabled));
    }

    public Task InitializeAsync() => Fixture.TryStartIis(this, IisAppType.AspNetCoreOutOfProcess, sendHealthCheck: false);

    public Task DisposeAsync() => Task.CompletedTask;

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    // We test the baggage routes because they create additional spans,
    // so we actually _will_ have some spans sent back, instead of having to wait an arbitrary time for nothing to appear
    [InlineData("/otel-baggage/get-baggage", 200)]
    [InlineData("/otel-baggage/set-current/foo_case_sensitive_key/overwrite_value", 200)]
    public async Task DoesNotCreateAspNetCoreSpans(string path, int statusCode)
    {
        // Adding baggage, but it won't actually be extracted
        var headers = new Dictionary<string, string>
        {
            { "baggage", "foo_case_sensitive_key=value_to_be_replaced" },
        };

        var spans = await GetWebServerSpans(path, Fixture.Agent, Fixture.HttpPort, (HttpStatusCode)statusCode, filterServerSpans: false, expectedSpanCount: 1, httpMethod: HttpMethod.Get, headers: headers);
        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: nameof(AspNetCoreIisNetFrameworkMvcTestsDisabled), isExternalSpan: false);

        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

        // Overriding the type name here as we have multiple test classes in the file
        // Ensures that we get nice file nesting in Solution Explorer
        await Verifier.Verify(spans, settings)
                      .DisableRequireUniquePrefix()
                      .UseMethodName("_")
                      .UseTypeName(nameof(AspNetCoreIisNetFrameworkMvcTestsDisabled)); // the output are the same regardless of 2.1/2.2
    }
}

[UsesVerify]
public abstract class AspNetCoreIisNetFrameworkMvcResourceBasedSamplingTest : AspNetCoreNetFrameworkIisMvcTestsBase, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;
    private readonly string _testName;

    protected AspNetCoreIisNetFrameworkMvcResourceBasedSamplingTest(string sampleName, IisFixture fixture, ITestOutputHelper output)
        : base(sampleName, fixture, output)
    {
        _iisFixture = fixture;
        _testName = nameof(AspNetCoreIisNetFrameworkMvcResourceBasedSamplingTest);
        SetServiceName(_testName);

        // These test resource-based sampling on the parent ASP.NET span.
        SetEnvironmentVariable(ConfigurationKeys.CustomSamplingRules, """[{"sample_rate":0.0, "service":"*", "resource":"GET /ping"}]""");
        SetEnvironmentVariable(ConfigurationKeys.CustomSamplingRulesFormat, SamplingRulesFormat.Glob); // for ease of use
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "LinuxUnsupported")]
    public async Task TestSampledSpan()
    {
        var path = "/ping";
        var statusCode = HttpStatusCode.OK;
        var spans = await GetWebServerSpans(path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode, expectedSpanCount: 1);

        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: _testName, isExternalSpan: false);

        var settings = VerifyHelper.GetSpanVerifierSettings();

        // Overriding the type name here as we have multiple test classes in the file
        // Ensures that we get nice file nesting in Solution Explorer
        await Verifier.Verify(spans, settings)
                      .DisableRequireUniquePrefix()
                      .UseMethodName("_")
                      .UseTypeName(_testName);
    }

    public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetCoreOutOfProcess);

    public Task DisposeAsync() => Task.CompletedTask;
}

[UsesVerify]
public abstract class AspNetCoreIisNetFrameworkMvcTests : AspNetCoreNetFrameworkIisMvcTestsBase, IAsyncLifetime
{
    private readonly string _testName;

    protected AspNetCoreIisNetFrameworkMvcTests(string sampleName, IisFixture fixture, ITestOutputHelper output)
        : base(sampleName, fixture, output)
    {
        _testName = nameof(AspNetCoreIisNetFrameworkMvcTests);
        SetServiceName(_testName);
    }

    public Task InitializeAsync() => Fixture.TryStartIis(this, IisAppType.AspNetCoreOutOfProcess);

    public Task DisposeAsync() => Task.CompletedTask;

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "LinuxUnsupported")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(Data))]
    public async Task MeetsAllAspNetCoreMvcExpectations(string path, int statusCode)
    {
        var spans = await GetWebServerSpans(path, Fixture.Agent, Fixture.HttpPort, (HttpStatusCode)statusCode, expectedSpanCount: 1);
        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: _testName, isExternalSpan: false);

        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

        // Overriding the type name here as we have multiple test classes in the file
        await Verifier.Verify(spans, settings)
                      .DisableRequireUniquePrefix()
                      .UseMethodName("_")
                      .UseTypeName(_testName);
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "LinuxUnsupported")]
    [Trait("RunOnWindows", "True")]
    public async Task ExtractsContext()
    {
        ulong traceId = 123456789;
        var parentId = 987654321;
        var path = "/ping";
        var headers = new Dictionary<string, string>
        {
            ["x-datadog-trace-id"] = traceId.ToString(),
            ["x-datadog-parent-id"] = parentId.ToString(),
            ["x-datadog-sampling-priority"] = "1",
        };
        var statusCode = HttpStatusCode.OK;
        var spans = await GetWebServerSpans(path, Fixture.Agent, Fixture.HttpPort, statusCode, expectedSpanCount: 1, headers: headers);
        spans.Should().AllSatisfy(x => x.TraceId.Should().Be(traceId));
        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: _testName, isExternalSpan: false);

        var settings = VerifyHelper.GetSpanVerifierSettings();

        // Overriding the type name here as we have multiple test classes in the file
        await Verifier.Verify(spans, settings)
                      .DisableRequireUniquePrefix()
                      .UseMethodName("_")
                      .UseTypeName(_testName);
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [SkippableTheory]
    [InlineData("/")]
    [InlineData("/delay/0")]
    public async Task MeetsAllAspNetCoreMvcExpectationsWithIncorrectMethod(string path)
    {
        var spans = await GetWebServerSpans(path, Fixture.Agent, Fixture.HttpPort, HttpStatusCode.NotFound, expectedSpanCount: 1, httpMethod: HttpMethod.Post);

        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: _testName, isExternalSpan: false);

        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath);

        // Overriding the type name here as we have multiple test classes in the file
        await Verifier.Verify(spans, settings)
                      .DisableRequireUniquePrefix()
                      .UseMethodName("WrongMethod")
                      .UseTypeName(_testName);
    }

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData("/", 200)]
    [InlineData("/not-found", 404)]
    [InlineData("/bad-request", 500)]
    public async Task BaggageInSpanTags(string path, int statusCode)
    {
        var headers = new Dictionary<string, string>
        {
            { "baggage", "user.id=doggo" },
        };

        var spans = await GetWebServerSpans(path, Fixture.Agent, Fixture.HttpPort, (HttpStatusCode)statusCode, expectedSpanCount: 1, httpMethod: HttpMethod.Get, headers: headers);
        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: _testName, isExternalSpan: false);

        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

        // Overriding the type name here as we have multiple test classes in the file
        // Ensures that we get nice file nesting in Solution Explorer
        await Verifier.Verify(spans, settings)
                      .DisableRequireUniquePrefix()
                      .UseMethodName("WithBaggage")
                      .UseTypeName(_testName);
    }

    // There's a bug with Baggage context management in aspnetcore < 5 which causes leakage
    // across requests. For now, just don't run the tests that are susceptible to the issue
    // https://github.com/dotnet/aspnetcore/issues/13991
    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [InlineData("/otel-baggage/clear-baggage", 200)]
    // [InlineData("/otel-baggage/get-baggage", 200)]
    [InlineData("/otel-baggage/get-baggage-name/foo_case_sensitive_key", 200)]
    // [InlineData("/otel-baggage/get-current", 200)]
    // [InlineData("/otel-baggage/get-enumerator", 200)]
    [InlineData("/otel-baggage/remove-baggage/remove_me_key", 200)]
    // [InlineData("/otel-baggage/set-baggage/foo_case_sensitive_key/overwrite_value", 200)]
    [InlineData("/otel-baggage/set-baggage/new_key/new_value", 200)]
    // [InlineData("/otel-baggage/set-baggage-items/foo_case_sensitive_key/overwrite_value", 200)]
    // [InlineData("/otel-baggage/set-baggage-items/new_key/new_value", 200)]
    [InlineData("/otel-baggage/set-current/foo_case_sensitive_key/overwrite_value", 200)]
    [InlineData("/otel-baggage/set-current/new_key/new_value", 200)]
    public async Task OtelBaggageApiIntegration(string path, int statusCode)
    {
        string[] baggageItems = [
            "foo_case_sensitive_key=value_to_be_replaced",
            "unused_key=unused_value",
            "FOO_CASE_SENSITIVE_KEY=UNTOUCHED",
            "remove_me_key=remove_me_value",
        ];
        var headers = new Dictionary<string, string>
        {
            { "baggage", string.Join(",", baggageItems) },
        };

        var spans = await GetWebServerSpans(path, Fixture.Agent, Fixture.HttpPort, (HttpStatusCode)statusCode, filterServerSpans: false, expectedSpanCount: 2, httpMethod: HttpMethod.Get, headers: headers);
        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: _testName, isExternalSpan: false);

        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

        // Overriding the type name here as we have multiple test classes in the file
        // Ensures that we get nice file nesting in Solution Explorer
        await Verifier.Verify(spans, settings)
                      .DisableRequireUniquePrefix()
                      .UseMethodName("OTelBaggageApi")
                      .UseTypeName(_testName);
    }
}

[UsesVerify]
public abstract class AspNetCoreNetFrameworkIisMvcTestsBase : TracingIntegrationTest, IClassFixture<IisFixture>
{
    private static readonly HashSet<string> ExcludeTags =
    [
        "datadog-header-tag",
        "http.request.headers.sample_correlation_identifier",
        "http.response.headers.sample_correlation_identifier",
        "http.response.headers.server",
        "baggage.user.id",
        "baggage.session.id",
        "baggage.account.id"
    ];

    protected AspNetCoreNetFrameworkIisMvcTestsBase(string sampleName, IisFixture fixture, ITestOutputHelper output)
        : base(sampleName, output)
    {
        SetServiceVersion("1.0.0");
        SetEnvironmentVariable(ConfigurationKeys.HttpServerErrorStatusCodes, "400-403, 500-503");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.ExperimentalFeaturesEnabled, ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled);

        Fixture = fixture;
    }

    protected IisFixture Fixture { get; }

    public static TheoryData<string, int> Data() => new()
    {
        { "/", 200 },
        { "/delay/0", 200 },
        { "/api/delay/0", 200 },
        { "/not-found", 404 },
        { "/status-code/203", 203 },
        { "/status-code/500", 500 },
        { "/status-code-string/[200]", 500 },
        { "/bad-request", 500 },
        { "/status-code/402", 402 },
        { "/ping", 200 },
        { "/branch/ping", 200 },
        { "/branch/not-found", 404 },
        { "/handled-exception", 500 },
    };

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
        span.Name switch
        {
            "aspnet_core.request" => span.IsAspNetCore(metadataSchemaVersion, ExcludeTags),
            _ => Result.DefaultSuccess,
        };
}
#endif
