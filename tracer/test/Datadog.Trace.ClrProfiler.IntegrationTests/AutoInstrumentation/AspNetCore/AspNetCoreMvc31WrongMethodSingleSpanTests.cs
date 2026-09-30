// <copyright file="AspNetCoreMvc31WrongMethodSingleSpanTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Only testing a single specific TFM just to reduce overhead
#if NET8_0
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore;

public class AspNetCoreMvc31WrongMethodSingleSpanTests : AspNetCoreMvcWrongMethodTestBase
{
    public AspNetCoreMvc31WrongMethodSingleSpanTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
        : base(nameof(AspNetCoreMvc31WrongMethodSingleSpanTests), "AspNetCoreMvc31", fixture, output, singleSpan: true)
    {
    }

    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    [SkippableTheory]
    [InlineData("/")]
    [InlineData("/delay/0")]
    public async Task MeetsAllAspNetCoreMvcExpectationsWithIncorrectMethod(string path)
    {
        await TestIncorrectMethod(path);
    }
}
#endif
