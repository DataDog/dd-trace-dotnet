// <copyright file="AspNetCoreIpCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore;

public abstract class AspNetCoreIpCollectionTests : AspNetCoreMvcTestBase
{
    private readonly string _testName;

    protected AspNetCoreIpCollectionTests(AspNetCoreTestFixture fixture, ITestOutputHelper output, string sampleName)
        : base(sampleName, fixture, output, enableRouteTemplateResourceNames: true)
    {
        _testName = GetTestName(sampleName);
        SetEnvironmentVariable(Configuration.ConfigurationKeys.IpHeaderEnabled, "1");
    }

    public static TheoryData<string, int> IpData() => new()
    {
        { "/", 200 },
        { "/not-found", 404 },
        { "/status-code/203", 203 },
        { "/bad-request", 500 },
        { "/branch/not-found", 404 },
        { "/handled-exception", 500 },
    };

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(IpData), MemberType = typeof(AspNetCoreIpCollectionTests))]
    public async Task CollectsIpWhenEnabled(string path, HttpStatusCode statusCode)
    {
        await Fixture.TryStartApp(this);

        var spans = await Fixture.WaitForSpans(path);
        ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: $"Samples.{EnvironmentHelper.SampleName}", isExternalSpan: false);

        var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
        var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

        // Overriding the type name here as we have multiple test classes in the file
        // Ensures that we get nice file nesting in Solution Explorer
        await Verifier.Verify(spans, settings)
                      .UseMethodName("IpCollection")
                      .UseTypeName(_testName);
    }

#if NETCOREAPP2_1
    public class AspNetCoreMvc21IpCollectionTests : AspNetCoreIpCollectionTests
    {
        public AspNetCoreMvc21IpCollectionTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "AspNetCoreMvc21")
        {
        }
    }
#endif

#if NETCOREAPP3_1
    public class AspNetCoreMvc31IpCollectionTests : AspNetCoreIpCollectionTests
    {
        public AspNetCoreMvc31IpCollectionTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "AspNetCoreMvc31")
        {
        }
    }
#endif

#if NET6_0_OR_GREATER
    public class AspNetCoreMvcMinimapApiCollectionTests : AspNetCoreIpCollectionTests
    {
        public AspNetCoreMvcMinimapApiCollectionTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "AspNetCoreMinimalApis")
        {
        }
    }
#endif
}

#endif
