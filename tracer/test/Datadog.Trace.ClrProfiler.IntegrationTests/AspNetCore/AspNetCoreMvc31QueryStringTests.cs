// <copyright file="AspNetCoreMvc31QueryStringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc30TestsNoQueryString : AspNetCoreMvc31QueryStringTests
    {
        public AspNetCoreMvc30TestsNoQueryString(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableQueryStringReporting: false)
        {
        }
    }

    public class AspNetCoreMvc30TestsQueryString : AspNetCoreMvc31QueryStringTests
    {
        public AspNetCoreMvc30TestsQueryString(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class AspNetCoreMvc30TestsRegexQueryString : AspNetCoreMvc31QueryStringTests
    {
        public AspNetCoreMvc30TestsRegexQueryString(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output, queryStringRegex: @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|authentic\d*)(?:\s*=[^&]+|""\s*:\s*""[^""]+"")|[a-z0-9\._\-]{100,}")
        {
        }
    }

    public abstract class AspNetCoreMvc31QueryStringTests : AspNetCoreMvcTestBase
    {
        private readonly bool? _enableQueryStringReporting;
        private readonly string _queryStringRegex;
        private readonly string _testName;

        protected AspNetCoreMvc31QueryStringTests(AspNetCoreTestFixture fixture, ITestOutputHelper output, bool? enableQueryStringReporting = null, string queryStringRegex = null)
            : base("AspNetCoreMvc31", fixture, output, enableRouteTemplateResourceNames: true)
        {
            _enableQueryStringReporting = enableQueryStringReporting;
            _queryStringRegex = queryStringRegex;
            _testName = GetTestName(nameof(AspNetCoreMvc31QueryStringTests));
            if (enableQueryStringReporting != null)
            {
                SetEnvironmentVariable(ConfigurationKeys.QueryStringReportingEnabled, enableQueryStringReporting.ToString());
            }

            if (queryStringRegex != null)
            {
                SetEnvironmentVariable(ConfigurationKeys.ObfuscationQueryStringRegex, queryStringRegex);
            }
        }

        public static new TheoryData<string, int> Data() => new() { { "/?authentic1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2", 200 } };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task MeetsAllAspNetCoreMvcExpectations(string path, HttpStatusCode statusCode)
        {
            await Fixture.TryStartApp(this);

            var spans = await Fixture.WaitForSpans(path);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }

        protected override string GetTestName(string testName) => $"{testName}{(_enableQueryStringReporting != null && !_enableQueryStringReporting.GetValueOrDefault() ? ".DisableQueryString" : string.Empty)}{(_queryStringRegex != null ? ".WithCustomRegex" : string.Empty)}";
    }
}
#endif
