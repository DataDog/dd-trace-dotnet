// <copyright file="VSTestEmptySessionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public class VSTestEmptySessionTests : TestingFrameworkEvpTest
{
    private const string NoMatchingTest = "NoMatchingTest";
    private const string PassingTest = "Samples.XUnitTests.TestSuite.SimplePassTest";

    public VSTestEmptySessionTests(ITestOutputHelper output)
        : base("XUnitTests", output)
    {
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled, "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.GitUploadEnabled, "0");
    }

    [SkippableTheory]
    [InlineData(NoMatchingTest, TestTags.StatusSkip)]
    [InlineData(PassingTest, TestTags.StatusPass)]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task SessionIsSkippedWhenVSTestRunsNoTests(string testName, string expectedStatus)
    {
        // The testhost connects to the session in vstest.console over IPC.
        SkipIfVSTestCannotShareNamedMutexes();
        var sessions = new ConcurrentQueue<MockCIVisibilityTestModule>();
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var @event in payload.Events ?? [])
                {
                    if (@event.Type == SpanTypes.TestSession)
                    {
                        sessions.Enqueue(JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(@event.Content.ToString()));
                    }
                }
            }
        };

        using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, arguments: $"/TestCaseFilter:FullyQualifiedName={testName}");

        var session = sessions.Should().ContainSingle().Subject;
        session.Meta[TestTags.Status].Should().Be(expectedStatus);
        if (expectedStatus == TestTags.StatusSkip)
        {
            session.Meta[TestTags.SkipReason].Should().Be(TestTags.SkipReasonNoTestsExecuted);
            session.Meta[TestTags.SessionEmptyReason].Should().Be(TestTags.SessionEmptyReasonZeroTests);
        }
        else
        {
            session.Meta.Should().NotContainKey(TestTags.SkipReason).And.NotContainKey(TestTags.SessionEmptyReason);
        }
    }

    [SkippableTheory]
    [InlineData(NoMatchingTest, 0)]
    [InlineData(PassingTest, 1)]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task VSTestSendsExecutedTestsToTheParentSession(string testName, long expectedExecutedTests)
    {
        // As with dotnet test, the session belongs to a parent process and vstest.console reports to it over IPC.
        SkipIfVSTestCannotShareNamedMutexes();
        InjectSession(out var sessionId, out _, out _, out _, out _, out _, out _);
        using var received = new ManualResetEventSlim();
        long? executedTests = null;
        using var ipcServer = new IpcServer($"session_{sessionId}");
        ipcServer.SetMessageReceivedCallback(
            message =>
            {
                if (message is SessionExecutedTestsMessage executedTestsMessage)
                {
                    executedTests = executedTestsMessage.ExecutedTests;
                    received.Set();
                }
            });

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunDotnetTestSampleAndWaitForExit(agent, arguments: $"/TestCaseFilter:FullyQualifiedName={testName}");

        received.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        executedTests.Should().Be(expectedExecutedTests);
    }
}
