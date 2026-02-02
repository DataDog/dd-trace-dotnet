// <copyright file="AspNetCoreActivityTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore;

public abstract class AspNetCoreActivityTagsTests : AspNetCoreMvcTestBase
{
    protected AspNetCoreActivityTagsTests(AspNetCoreTestFixture fixture, ITestOutputHelper output, string sampleName, bool singleSpan = false)
        : base(sampleName, fixture, output, singleSpan ? AspNetCoreFeatureFlags.SingleSpan : AspNetCoreFeatureFlags.RouteTemplateResourceNames)
    {
        SetEnvironmentVariable("ADD_ACTIVITY_MIDDLEWARE", "1");
        SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.SingleSpanAspNetCoreEnabled, singleSpan.ToString());
        // Activity tag copying only happens when OTel is enabled
        SetEnvironmentVariable(ConfigurationKeys.OpenTelemetry.ActivityListenerEnabled, "1");
    }

    public static TheoryData<string> PathsToCheck() => new()
    {
        "/",
        "/not-found",
        "/status-code/203",
        "/bad-request",
        "/branch/not-found",
        "/handled-exception",
    };

    [SkippableTheory]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [MemberData(nameof(PathsToCheck), MemberType = typeof(AspNetCoreActivityTagsTests))]
    public async Task AddActivityTagsToRoot(string path)
    {
        await Fixture.TryStartApp(this);

        var spans = await Fixture.WaitForSpans(path);

        // we don't bother with snapshots as these are tested everywhere else already
        // we just want to make sure the tags are always there on the root span
        var rootSpans = spans.Where(x => x.ParentId is null);
        rootSpans.Should()
                 .AllSatisfy(x => x.Tags
                                   .Should()
                                   .Contain(new KeyValuePair<string, string>("pre_invoke", "value1"))
                                   .And.Contain(new KeyValuePair<string, string>("post_invoke", "value2")));
    }

    // Our activity listener doesn't support .NET Core 2.1 built-in version

#if NETCOREAPP3_1
    public class AspNetCoreMvc31ActivityTagsTests : AspNetCoreActivityTagsTests
    {
        public AspNetCoreMvc31ActivityTagsTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "AspNetCoreMvc31")
        {
        }
    }
#endif

#if NET6_0_OR_GREATER
    public class AspNetCoreMvcMinimalApiActivityTagsTests : AspNetCoreActivityTagsTests
    {
        public AspNetCoreMvcMinimalApiActivityTagsTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "AspNetCoreMinimalApis")
        {
        }
    }

    public class AspNetCoreSingleSpanActivityTagsTests : AspNetCoreActivityTagsTests
    {
        public AspNetCoreSingleSpanActivityTagsTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "AspNetCoreMvc31", singleSpan: true)
        {
        }
    }
#endif
}
#endif
