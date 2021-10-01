// <copyright file="AspNetAsyncHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNet
{
#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

    public class AspNetAsyncHandlerTestsCallsite : AspNetAsyncHandlerTests
    {
        public AspNetAsyncHandlerTestsCallsite()
            : base(enableCallTarget: false)
        {
        }
    }

    public class AspNetAsyncHandlerTestsCallTarget : AspNetAsyncHandlerTests
    {
        public AspNetAsyncHandlerTestsCallTarget()
            : base(enableCallTarget: true)
        {
        }
    }

    [NonParallelizable]
    public abstract class AspNetAsyncHandlerTests : IisTestsBase
    {
        protected AspNetAsyncHandlerTests(bool enableCallTarget)
            : base("AspNetAsyncHandler", @"test\test-applications\aspnet", IisAppType.AspNetIntegrated, "/shutdown")
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);
        }

        [Test]
        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [Property("LoadFromGAC", "True")]
        public async Task SubmitsTraces()
        {
            var spans = await GetWebServerSpans("/test", Agent, HttpPort, expectedHttpStatusCode: HttpStatusCode.OK, expectedSpanCount: 2, filterServerSpans: false);

            spans.Should().HaveCount(2);

            var customSpan = spans.First(s => s.Name == "HttpHandler");

            customSpan.ParentId.Should().NotBeNull("traces should be correlated");
        }
    }
#endif
}
