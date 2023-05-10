// <copyright file="AspNetCoreMvcTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [UsesVerify]
    public abstract class AspNetCoreMvcTestBase : TracingIntegrationTest, IClassFixture<AspNetCoreTestFixture>, IDisposable
    {
        protected const string HeaderName1WithMapping = "datadog-header-name";
        protected const string HeaderName1UpperWithMapping = "DATADOG-HEADER-NAME";
        protected const string HeaderTagName1WithMapping = "datadog-header-tag";
        protected const string HeaderValue1 = "asp-net-core";
        protected const string HeaderName2 = "sample.correlation.identifier";
        protected const string HeaderValue2 = "0000-0000-0000";
        protected const string HeaderName3 = "Server";
        protected const string HeaderValue3 = "Kestrel";

        private static readonly HashSet<string> ExcludeTags = new HashSet<string>
        {
            "datadog-header-tag",
            "http.request.headers.sample_correlation_identifier",
            "http.response.headers.sample_correlation_identifier",
            "http.response.headers.server",
        };

        private readonly bool _enableRouteTemplateResourceNames;

        protected AspNetCoreMvcTestBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper output, bool enableRouteTemplateResourceNames)
            : base(sampleName, output)
        {
            _enableRouteTemplateResourceNames = enableRouteTemplateResourceNames;
            SetEnvironmentVariable(ConfigurationKeys.HeaderTags, $"{HeaderName1UpperWithMapping}:{HeaderTagName1WithMapping},{HeaderName2},{HeaderName3}");
            SetEnvironmentVariable(ConfigurationKeys.HttpServerErrorStatusCodes, "400-403, 500-503");

            SetServiceVersion("1.0.0");

            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());

            Fixture = fixture;
            Fixture.SetOutput(output);
        }

        protected AspNetCoreTestFixture Fixture { get; }

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

        public override void Dispose()
        {
            Fixture.SetOutput(null);
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet_core.request" => span.IsAspNetCore(metadataSchemaVersion, ExcludeTags),
                "aspnet_core_mvc.request" => span.IsAspNetCoreMvc(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        protected virtual string GetTestName(string testName)
        {
            return testName
                 + (_enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF");
        }
    }
}
#endif
