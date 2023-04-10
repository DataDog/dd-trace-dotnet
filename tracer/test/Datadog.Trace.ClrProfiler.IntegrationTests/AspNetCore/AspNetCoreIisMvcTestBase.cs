// <copyright file="AspNetCoreIisMvcTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP

using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [UsesVerify]
    public abstract class AspNetCoreIisMvcTestBase : TracingIntegrationTest, IClassFixture<IisFixture>
    {
        private readonly bool _inProcess;
        private readonly bool _enableRouteTemplateResourceNames;

        protected AspNetCoreIisMvcTestBase(string sampleName, IisFixture fixture, ITestOutputHelper output, bool inProcess, bool enableRouteTemplateResourceNames)
            : base(sampleName, output)
        {
            _inProcess = inProcess;
            _enableRouteTemplateResourceNames = enableRouteTemplateResourceNames;
            SetEnvironmentVariable(ConfigurationKeys.HttpServerErrorStatusCodes, "400-403, 500-503");
            EnableDebugMode();

            SetServiceVersion("1.0.0");

            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());

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
            metadataSchemaVersion switch
            {
                _ => span.Type switch
                {
                    "aspnet_core.request" => span.IsAspNetCoreV0(),
                    "aspnet_core_mvc.request" => span.IsAspNetCoreMvcV0(),
                    _ => Result.DefaultSuccess,
                }
            };

        protected string GetTestName(string testName)
        {
            return testName
                 + (_inProcess ? ".InProcess" : ".OutOfProcess")
                 + (_enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF");
        }
    }
}
#endif
