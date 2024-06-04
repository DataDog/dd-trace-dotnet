// <copyright file="SeleniumTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[UsesVerify]
public class SeleniumTests : TestingFrameworkEvpTest
{
    private readonly GacFixture _gacFixture;

    public SeleniumTests(ITestOutputHelper output)
        : base("Selenium", output)
    {
        SetServiceName("xunit-selenium-tests");
        SetServiceVersion("1.0.0");
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.Selenium), MemberType = typeof(PackageVersions))]
    [Trait("RunOnWindows", "True")]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task Injection(string packageVersion)
    {
        SkipOn.Platform(SkipOn.PlatformValue.Linux);
        SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.Windows, SkipOn.ArchitectureValue.X86);

        var tests = new List<MockCIVisibilityTest>();
        var testSuites = new List<MockCIVisibilityTestSuite>();
        var testModules = new List<MockCIVisibilityTestModule>();

        // Inject session
        var sessionId = RandomIdGenerator.Shared.NextSpanId();
        var sessionCommand = "test command";
        var sessionWorkingDirectory = Path.GetTempPath();
        SetEnvironmentVariable(HttpHeaderNames.TraceId.Replace(".", "_").Replace("-", "_").ToUpperInvariant(), sessionId.ToString(CultureInfo.InvariantCulture));
        SetEnvironmentVariable(HttpHeaderNames.ParentId.Replace(".", "_").Replace("-", "_").ToUpperInvariant(), sessionId.ToString(CultureInfo.InvariantCulture));
        SetEnvironmentVariable(TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable, sessionCommand);
        SetEnvironmentVariable(TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable, sessionWorkingDirectory);

        const string gitRepositoryUrl = "git@github.com:DataDog/dd-trace-dotnet.git";
        const string gitBranch = "main";
        const string gitCommitSha = "3245605c3d1edc67226d725799ee969c71f7632b";
        SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitRepository, gitRepositoryUrl);
        SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitBranch, gitBranch);
        SetEnvironmentVariable(CIEnvironmentValues.Constants.DDGitCommitSha, gitCommitSha);
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");

        var codeCoverageReceived = new StrongBox<bool>(false);
        var name = $"session_{sessionId}";
        using var ipcServer = new IpcServer(name);
        ipcServer.SetMessageReceivedCallback(
            o =>
            {
                codeCoverageReceived.Value = codeCoverageReceived.Value || o is SessionCodeCoverageMessage;
            });

        using var agent = MockTracerAgent.Create(Output);

        const string correlationId = "2e8a36bda770b683345957cc6c15baf9";
        agent.EventPlatformProxyPayloadReceived += (sender, e) =>
        {
            // This is a special endpoint just to return the rum web page
            if (e.Value.PathAndQuery.EndsWith("rumpage"))
            {
                // Mocking RUM objects to test the selenium integration
                e.Value.Response = new MockTracerResponse(
                    """
                    <html>
                        <head>
                            <title>Test Page</title>
                            <script>
                                window.DD_RUM = {
                                    stopSession: function() {}
                                };
                            </script>
                        </head>
                        <body>
                            <h1>Test page</h1>
                        </body>
                    </html>
                    """) { ContentType = "text/html" };
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
            {
                e.Value.Response = new MockTracerResponse("""{"data":{"id":"b5a855bffe6c0b2ae5d150fb6ad674363464c816","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"efd_enabled":false,"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}} """);
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable"))
            {
                e.Value.Response = new MockTracerResponse($"{{\"data\":[],\"meta\":{{\"correlation_id\":\"{correlationId}\"}}}}");
                return;
            }

            if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                e.Value.Headers["Content-Encoding"].Should().Be("gzip");

                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                if (payload.Events?.Length > 0)
                {
                    foreach (var @event in payload.Events)
                    {
                        if (@event.Content.ToString() is { } eventContent)
                        {
                            if (@event.Type == SpanTypes.Test)
                            {
                                tests.Add(JsonConvert.DeserializeObject<MockCIVisibilityTest>(eventContent));
                            }
                            else if (@event.Type == SpanTypes.TestSuite)
                            {
                                testSuites.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestSuite>(eventContent));
                            }
                            else if (@event.Type == SpanTypes.TestModule)
                            {
                                testModules.Add(JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(eventContent));
                            }
                        }
                    }
                }
            }
        };

        SetEnvironmentVariable("SAMPLES_SELENIUM_TEST_URL", $"http://127.0.0.1:{agent.Port}/evp_proxy/v4/rumpage");
        var sampleAppPath = EnvironmentHelper.GetTestCommandForSampleApplicationPath(packageVersion);
        var sampleFolder = Path.GetDirectoryName(sampleAppPath);
        using var processResult = await RunDotnetTestSampleAndWaitForExit(
                                      agent,
                                      packageVersion: packageVersion,
                                      arguments: $"--settings:\"{Path.Combine(sampleFolder, "ci.runsettings")}\"");

        // Check if we have the data
        using var s = new AssertionScope();
        tests.Should().HaveCount(1);
        testSuites.Should().HaveCount(1);
        testModules.Should().HaveCount(1);

        var test = tests[0];
        test.Meta.GetValueOrDefault(BrowserTags.BrowserDriver).Should().Be("selenium");
        test.Meta.GetValueOrDefault(BrowserTags.BrowserDriverVersion).Should().NotBeEmpty();
        test.Meta.GetValueOrDefault(BrowserTags.BrowserName).Should().Be("chrome");
        test.Meta.GetValueOrDefault(BrowserTags.BrowserVersion).Should().NotBeEmpty();

        var settings = VerifyHelper.GetCIVisibilitySpanVerifierSettings();
        settings.UseTextForParameters("all_versions");
        settings.DisableRequireUniquePrefix();
        await Verifier.Verify(
            tests
               .OrderBy(s => s.Resource)
               .ThenBy(s => s.Meta.GetValueOrDefault(TestTags.Parameters)),
            settings);

        // check if we received code coverage information at session level
        codeCoverageReceived.Value.Should().BeTrue();
    }

    public override void Dispose()
    {
        _gacFixture.RemoveAssembliesFromGac();
    }
}

#endif
