// <copyright file="VersionMismatchTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Globalization;
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
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

#if NET6_0_OR_GREATER
public class VersionMismatchTests : TestingFrameworkEvpTest
{
    public VersionMismatchTests(ITestOutputHelper output)
        : base("CIVisibilityVersionMismatch", output)
    {
        SetServiceName("version-mismatch-tests");
        SetServiceVersion("1.0.0");
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task Submit()
    {
        var customSpans = new List<MockSpan>();
        var tests = new List<MockCIVisibilityTest>();
        var testSuites = new List<MockCIVisibilityTestSuite>();
        var testModules = new List<MockCIVisibilityTestModule>();

        // Inject session
        var sessionId = RandomIdGenerator.Shared.NextSpanId();
        var sessionCommand = "test command";
        var sessionWorkingDirectory = "C:\\evp_demo\\working_directory";
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

        var codeCoverageReceived = new StrongBox<bool>(false);
        var name = $"session_{sessionId}";
        using var ipcServer = new IpcServer(name);
        ipcServer.SetMessageReceivedCallback(
            o =>
            {
                codeCoverageReceived.Value = codeCoverageReceived.Value || o is SessionCodeCoverageMessage;
            });

        try
        {
            SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");

            using var agent = EnvironmentHelper.GetMockAgent();
            const string correlationId = "2e8a36bda770b683345957cc6c15baf9";
            agent.EventPlatformProxyPayloadReceived += (sender, e) =>
            {
                if (e.Value.PathAndQuery.EndsWith("api/v2/libraries/tests/services/setting"))
                {
                    e.Value.Response = new MockTracerResponse("""{"data":{"id":"b5a855bffe6c0b2ae5d150fb6ad674363464c816","type":"ci_app_tracers_test_service_settings","attributes":{"code_coverage":false,"efd_enabled":false,"flaky_test_retries_enabled":false,"itr_enabled":true,"require_git":false,"tests_skipping":true}}} """, 200);
                    return;
                }

                if (e.Value.PathAndQuery.EndsWith("api/v2/ci/tests/skippable"))
                {
                    e.Value.Response = new MockTracerResponse($"{{\"data\":[],\"meta\":{{\"correlation_id\":\"{correlationId}\"}}}}", 200);
                    return;
                }

                if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
                {
                    var lstEvents = new List<string>();
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
                                else
                                {
                                    customSpans.Add(JsonConvert.DeserializeObject<MockSpan>(eventContent));
                                }
                            }
                        }
                    }
                }
            };

            using var processResult = await RunDotnetTestSampleAndWaitForExit(
                                          agent,
                                          arguments: "--collect:\"XPlat Code Coverage\"");

            processResult.ExitCode.Should().Be(0);

            // Check the tests, suites and modules count
            tests.Should().ContainSingle();
            testSuites.Should().ContainSingle();
            testModules.Should().ContainSingle();

            // Check the custom spans count comming from mismatched version of Datadog.Trace
            customSpans.Should().ContainSingle();

            // check if we received code coverage information at session level
            codeCoverageReceived.Value.Should().BeTrue();
        }
        catch
        {
            WriteSpans(tests);
            throw;
        }
    }
}
#endif
