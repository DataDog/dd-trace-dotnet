// <copyright file="AspNetCoreMvcResourceBasedSamplingTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore;

// Only testing a single specific TFM for each, just to reduce overhead
#if NETCOREAPP2_1
public class AspNetCoreMvc21ResourceBasedSamplingTests : AspNetCoreMvcResourceBasedSamplingTestBase
{
    public AspNetCoreMvc21ResourceBasedSamplingTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreMvc21ResourceBasedSamplingTests), "AspNetCoreMvc21", fixture, output)
    {
    }
}

// Note that ASP.NET Core 2.1 does not support in-process hosting

[Collection("IisTests")]
public class AspNetCoreIisMvc21MvcResourceBasedSamplingTests : AspNetCoreIisMvcResourceBasedSamplingTestBase
{
    public AspNetCoreIisMvc21MvcResourceBasedSamplingTests(IisFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreIisMvc21MvcResourceBasedSamplingTests), "AspNetCoreMvc21", fixture, output, inProcess: false)
    {
    }
}
#elif NETCOREAPP3_0
public class AspNetCoreMvc30ResourceBasedSamplingTests : AspNetCoreMvcResourceBasedSamplingTestBase
{
    public AspNetCoreMvc30ResourceBasedSamplingTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreMvc30ResourceBasedSamplingTests), "AspNetCoreMvc30", fixture, output)
    {
    }
}

[Collection("IisTests")]
public class AspNetCoreIisMvc30MvcResourceBasedSamplingInProcessTests : AspNetCoreIisMvcResourceBasedSamplingTestBase
{
    public AspNetCoreIisMvc30MvcResourceBasedSamplingInProcessTests(IisFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreIisMvc30MvcResourceBasedSamplingInProcessTests), "AspNetCoreMvc30", fixture, output, inProcess: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetCoreIisMvc30MvcResourceBasedSamplingOutOfProcessTests : AspNetCoreIisMvcResourceBasedSamplingTestBase
{
    public AspNetCoreIisMvc30MvcResourceBasedSamplingOutOfProcessTests(IisFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreIisMvc30MvcResourceBasedSamplingOutOfProcessTests), "AspNetCoreMvc30", fixture, output, inProcess: false)
    {
    }
}
#elif NETCOREAPP3_1
public class AspNetCoreMvc31ResourceBasedSamplingTests : AspNetCoreMvcResourceBasedSamplingTestBase
{
    public AspNetCoreMvc31ResourceBasedSamplingTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreMvc31ResourceBasedSamplingTests), "AspNetCoreMvc31", fixture, output)
    {
    }
}

[Collection("IisTests")]
public class AspNetCoreIisMvc31MvcResourceBasedSamplingInProcessTests : AspNetCoreIisMvcResourceBasedSamplingTestBase
{
    public AspNetCoreIisMvc31MvcResourceBasedSamplingInProcessTests(IisFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreIisMvc31MvcResourceBasedSamplingInProcessTests), "AspNetCoreMvc31", fixture, output, inProcess: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetCoreIisMvc31MvcResourceBasedSamplingOutOfProcessTests : AspNetCoreIisMvcResourceBasedSamplingTestBase
{
    public AspNetCoreIisMvc31MvcResourceBasedSamplingOutOfProcessTests(IisFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreIisMvc31MvcResourceBasedSamplingOutOfProcessTests), "AspNetCoreMvc31", fixture, output, inProcess: false)
    {
    }
}
#elif NET8_0
public class AspNetCoreMvc31ResourceBasedSamplingSingleSpanTests : AspNetCoreMvcResourceBasedSamplingTestBase
{
    public AspNetCoreMvc31ResourceBasedSamplingSingleSpanTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreMvc31ResourceBasedSamplingSingleSpanTests), "AspNetCoreMvc31", fixture, output, singleSpan: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetCoreIisMvc31MvcResourceBasedSamplingInProcessSingleSpanTests : AspNetCoreIisMvcResourceBasedSamplingTestBase
{
    public AspNetCoreIisMvc31MvcResourceBasedSamplingInProcessSingleSpanTests(IisFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreIisMvc31MvcResourceBasedSamplingInProcessSingleSpanTests), "AspNetCoreMvc31", fixture, output, inProcess: true, singleSpan: true)
    {
    }
}

[Collection("IisTests")]
public class AspNetCoreIisMvc31MvcResourceBasedSamplingOutOfProcessSingleSpanTests : AspNetCoreIisMvcResourceBasedSamplingTestBase
{
    public AspNetCoreIisMvc31MvcResourceBasedSamplingOutOfProcessSingleSpanTests(IisFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreIisMvc31MvcResourceBasedSamplingOutOfProcessSingleSpanTests), "AspNetCoreMvc31", fixture, output, inProcess: false, singleSpan: true)
    {
    }
}
#endif

[UsesVerify]
public abstract class AspNetCoreMvcResourceBasedSamplingTestBase : AspNetCoreMvcTestBase
{
    private readonly AspNetCoreTestFixture fixture;
    private readonly string _testName;

    public AspNetCoreMvcResourceBasedSamplingTestBase(string testName, string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper output, bool singleSpan = false)
        : base(sampleName, fixture, output, singleSpan ? AspNetCoreFeatureFlags.SingleSpan : AspNetCoreFeatureFlags.RouteTemplateResourceNames)
    {
        // These test resource-based sampling on the parent ASP.NET span (non-MVC) one.
        SetEnvironmentVariable(ConfigurationKeys.CustomSamplingRules, """[{"sample_rate":0.0, "service":"*", "resource":"GET /ping"}]""");
        SetEnvironmentVariable(ConfigurationKeys.CustomSamplingRulesFormat, SamplingRulesFormat.Glob); // for ease of use

        this.fixture = fixture;
        _testName = testName;
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task TestSampledSpan()
    {
        var path = "/ping";
        await fixture.TryStartApp(this);

        var spans = await fixture.WaitForSpans(path, HttpMethod.Get);
        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: EnvironmentHelper.FullSampleName, isExternalSpan: false);

        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath);

        // Overriding the type name here as we have multiple test classes in the file
        // Ensures that we get nice file nesting in Solution Explorer
        await Verifier.Verify(spans, settings)
                      .UseMethodName("_")
                      .UseTypeName(_testName);
    }
}

[UsesVerify]
public abstract class AspNetCoreIisMvcResourceBasedSamplingTestBase : AspNetCoreIisMvcTestBase, IAsyncLifetime
{
    private readonly IisFixture _iisFixture;
    private readonly string _testName;

    protected AspNetCoreIisMvcResourceBasedSamplingTestBase(string testName, string sampleName, IisFixture fixture, ITestOutputHelper output, bool inProcess, bool singleSpan = false)
        : base(sampleName, fixture, output, inProcess, singleSpan ? AspNetCoreFeatureFlags.SingleSpan : AspNetCoreFeatureFlags.RouteTemplateResourceNames)
    {
        _iisFixture = fixture;
        _testName = testName;

        // These test resource-based sampling on the parent ASP.NET span (non-MVC) one.
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
        foreach (var span in spans)
        {
            var result = ValidateIntegrationSpan(span, metadataSchemaVersion: "v0");
            Assert.True(result.Success, result.ToString());
        }

        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

        // Overriding the type name here as we have multiple test classes in the file
        // Ensures that we get nice file nesting in Solution Explorer
        await Verifier.Verify(spans, settings)
                      .UseMethodName("_")
                      .UseTypeName(_testName);
    }

    public Task InitializeAsync() => _iisFixture.TryStartIis(this, InProcess ? IisAppType.AspNetCoreInProcess : IisAppType.AspNetCoreOutOfProcess);

    public Task DisposeAsync() => Task.CompletedTask;
}
#endif
