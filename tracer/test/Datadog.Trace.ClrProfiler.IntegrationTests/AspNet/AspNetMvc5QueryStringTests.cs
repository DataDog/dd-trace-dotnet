// <copyright file="AspNetMvc5QueryStringTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5TestsQueryString : AspNetMvc5QueryStringTests
    {
        public AspNetMvc5TestsQueryString(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsQueryStringDisabled : AspNetMvc5QueryStringTests
    {
        public AspNetMvc5TestsQueryStringDisabled(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, false)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc5QueryStringTests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        protected AspNetMvc5QueryStringTests(IisFixture iisFixture, ITestOutputHelper output, bool enableQueryStringReporting)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, "true");
            SetEnvironmentVariable(ConfigurationKeys.QueryStringReportingEnabled, enableQueryStringReporting.ToString());

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _testName = nameof(AspNetMvc5QueryStringTests)
                      + (enableQueryStringReporting ? ".WithQueryString" : ".WithoutQueryString");
        }

        public static TheoryData<string, int> Data => new() { { "/?authentic1=val1&token=a0b21ce2-006f-4cc6-95d5-d7b550698482&key2=val2", 200 }, };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-mvc.request" => span.IsAspNetMvc(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode)
        {
            // Append virtual directory to the actual request
            var spans = await GetWebServerSpans(_iisFixture.VirtualApplicationPath + path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}

#endif
