// <copyright file="AspNetCoreNetFrameworkAssemblyBindingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreNetFramework21AssemblyBindingTests : AspNetCoreNetFrameworkAssemblyBindingTestBase
    {
        public AspNetCoreNetFramework21AssemblyBindingTests(ITestOutputHelper output)
            : base("AspNetCoreNetFramework21", expectedDiagnosticSourceVersion: "4.0.3.1", expectedAspNetCoreVersion: "2.1.7.0", output: output)
        {
        }
    }

    public class AspNetCoreNetFramework22AssemblyBindingTests : AspNetCoreNetFrameworkAssemblyBindingTestBase
    {
        public AspNetCoreNetFramework22AssemblyBindingTests(ITestOutputHelper output)
            : base("AspNetCoreNetFramework", expectedDiagnosticSourceVersion: "4.0.3.0", expectedAspNetCoreVersion: "2.2.0.0", output: output)
        {
        }
    }

    public abstract class AspNetCoreNetFrameworkAssemblyBindingTestBase : TestHelper
    {
        private readonly string _expectedDiagnosticSourceVersion;
        private readonly string _expectedAspNetCoreVersion;

        protected AspNetCoreNetFrameworkAssemblyBindingTestBase(
            string sampleName,
            string expectedDiagnosticSourceVersion,
            string expectedAspNetCoreVersion,
            ITestOutputHelper output)
            : base(sampleName, output)
        {
            _expectedDiagnosticSourceVersion = expectedDiagnosticSourceVersion;
            _expectedAspNetCoreVersion = expectedAspNetCoreVersion;
            SetEnvironmentVariable("DD_TRACE_ASPNETCORE_ENABLED", "true");
            SetEnvironmentVariable("ENABLE_MANUAL_TRACING_MIDDLEWARE", "false");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task EnablementPreservesAspNetCoreAndDiagnosticSourceBindings()
        {
            var disabledBindings = await GetAssemblyBindings(featureEnabled: false);
            var enabledBindings = await GetAssemblyBindings(featureEnabled: true);

            enabledBindings.Should().Equal(disabledBindings, "feature enablement must not alter application assembly binding");
            enabledBindings.Should().Contain($"System.Diagnostics.DiagnosticSource={_expectedDiagnosticSourceVersion}");
            enabledBindings.Should().Contain($"Microsoft.AspNetCore={_expectedAspNetCoreVersion}");
        }

        private async Task<string[]> GetAssemblyBindings(bool featureEnabled)
        {
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, featureEnabled.ToString());

            using (var fixture = new AspNetCoreTestFixture())
            {
                fixture.SetOutput(Output);
                await fixture.TryStartApp(this, sendHealthCheck: false);

                var startTime = DateTimeOffset.UtcNow;
                using (var client = new HttpClient())
                using (var response = await client.GetAsync($"http://localhost:{fixture.HttpPort}/diagnostics/assembly-bindings"))
                {
                    response.StatusCode.Should().Be(HttpStatusCode.OK);
                    var content = await response.Content.ReadAsStringAsync();

                    if (featureEnabled)
                    {
                        var spans = await fixture.Agent.WaitForSpansAsync(
                                        count: 1,
                                        timeoutInMilliseconds: 20_000,
                                        minDateTime: startTime,
                                        returnAllOperations: true);
                        spans.Should().ContainSingle(span => span.Name == "aspnet_core.request");
                    }

                    return content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }
    }
}

#endif
