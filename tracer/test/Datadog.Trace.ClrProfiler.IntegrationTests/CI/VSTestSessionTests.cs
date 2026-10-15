// <copyright file="VSTestSessionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public abstract class VSTestSessionTests : TestingFrameworkEvpTest
{
    private readonly string _integrationEnabledVariable;
    private readonly string _passingTestName;

    protected VSTestSessionTests(string sampleName, string integrationEnabledVariable, ITestOutputHelper output)
        : base(sampleName, output)
    {
        _integrationEnabledVariable = integrationEnabledVariable;
        _passingTestName = $"Samples.{sampleName}.TestSuite.SimplePassTest";
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.AgentlessEnabled, "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.IntelligentTestRunnerEnabled, "0");
        SetEnvironmentVariable(ConfigurationKeys.CIVisibility.GitUploadEnabled, "0");
    }

    [SkippableTheory]
    [InlineData(true, true, "skip", 0)]
    [InlineData(false, true, "pass", 1)]
    [InlineData(false, false, "pass", 0)]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    public async Task UsesRunnerCountsForSessionStatus(bool noTests, bool instrumentFramework, string expectedStatus, int expectedTestEvents)
    {
        SetEnvironmentVariable(_integrationEnabledVariable, instrumentFramework ? "1" : "0");
        var events = new ConcurrentBag<MockCIVisibilityEvent>();
        using var agent = EnvironmentHelper.GetMockAgent();
        agent.EventPlatformProxyPayloadReceived += (_, e) =>
        {
            if (e.Value.PathAndQuery.EndsWith("api/v2/citestcycle"))
            {
                var payload = JsonConvert.DeserializeObject<MockCIVisibilityProtocol>(e.Value.BodyInJson);
                foreach (var @event in payload.Events)
                {
                    events.Add(@event);
                }
            }
        };

        var testName = noTests ? "NoMatchingTest" : _passingTestName;
        using var result = await RunDotnetTestSampleAndWaitForExit(
            agent,
            arguments: $"/Platform:{EnvironmentTools.GetTestTargetPlatform()} /TestCaseFilter:FullyQualifiedName={testName}",
            forceVsTestParam: true);

        if (noTests)
        {
            result.StandardOutput.Should().Contain("No test matches");
        }
        else
        {
            result.StandardOutput.Should().MatchRegex(@"Passed:\s+1");
        }

        events.Count(e => e.Type == SpanTypes.Test).Should().Be(expectedTestEvents);
        var sessionEvent = events.Should().ContainSingle(e => e.Type == SpanTypes.TestSession).Subject;
        var session = JsonConvert.DeserializeObject<MockCIVisibilityTestModule>(sessionEvent.Content.ToString());
        session.Meta[TestTags.Status].Should().Be(expectedStatus);
        if (noTests)
        {
            session.Meta[TestTags.SkipReason].Should().Be("VSTest reported zero tests.");
            session.Meta[TestTags.SessionEmptyReason].Should().Be("zero_tests");
        }
        else
        {
            session.Meta.Should().NotContainKey(TestTags.SkipReason).And.NotContainKey(TestTags.SessionEmptyReason);
        }
    }
}
