// <copyright file="AspNetCoreMvc31WrongMethodTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc31WrongMethodTests : AspNetCoreMvcWrongMethodTestBase
    {
        public AspNetCoreMvc31WrongMethodTests(AspNetCoreMvcTestBase.AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(nameof(AspNetCoreMvc31WrongMethodTests), "AspNetCoreMvc31", fixture, output)
        {
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Fact]
        public async Task MeetsAllAspNetCoreMvcExpectationsWithIncorrectMethod()
        {
           await TestIncorrectMethod();
        }
    }
}
#endif
