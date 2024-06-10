// <copyright file="TestingFrameworkEvpTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public abstract class TestingFrameworkEvpTest : TestHelper
{
    private readonly GacFixture _gacFixture;

    protected TestingFrameworkEvpTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
        : base(sampleAppName, samplePathOverrides, output)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkEvpTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output, bool prependSamplesToAppName)
        : base(sampleAppName, samplePathOverrides, output, prependSamplesToAppName)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkEvpTest(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, output)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkEvpTest(EnvironmentHelper environmentHelper, ITestOutputHelper output)
        : base(environmentHelper, output)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected object? CIValues { get; private set; }

    public override void Dispose()
    {
        _gacFixture.RemoveAssembliesFromGac();
    }

    protected virtual void WriteSpans(List<MockCIVisibilityTest>? tests)
    {
        if (tests is null || tests.Count == 0)
        {
            return;
        }

        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
        sb.AppendLine("***********************************");

        var i = 0;
        foreach (var test in tests)
        {
            sb.Append($" {i++}) ");
            sb.Append($"TraceId={test.TraceId}, ");
            sb.Append($"SpanId={test.SpanId}, ");
            sb.Append($"Service={test.Service}, ");
            sb.Append($"Name={test.Name}, ");
            sb.Append($"Resource={test.Resource}, ");
            sb.Append($"Type={test.Type}, ");
            sb.Append($"Error={test.Error}");
            sb.AppendLine();
            sb.AppendLine("   Tags=");
            foreach (var kv in test.Meta)
            {
                sb.AppendLine($"       => {kv.Key} = {kv.Value}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("***********************************");
        Output.WriteLine(StringBuilderCache.GetStringAndRelease(sb));
    }

    protected virtual void WriteSpans(List<MockCIVisibilityTestSuite>? suites)
    {
        if (suites is null || suites.Count == 0)
        {
            return;
        }

        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
        sb.AppendLine("***********************************");

        var i = 0;
        foreach (var suite in suites)
        {
            sb.Append($" {i++}) ");
            sb.Append($"TestSuiteId={suite.TestSuiteId}, ");
            sb.Append($"Service={suite.Service}, ");
            sb.Append($"Name={suite.Name}, ");
            sb.Append($"Resource={suite.Resource}, ");
            sb.Append($"Type={suite.Type}, ");
            sb.Append($"Error={suite.Error}");
            sb.AppendLine();
            sb.AppendLine("   Tags=");
            foreach (var kv in suite.Meta)
            {
                sb.AppendLine($"       => {kv.Key} = {kv.Value}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("***********************************");
        Output.WriteLine(StringBuilderCache.GetStringAndRelease(sb));
    }

    protected virtual string AssertTargetSpanAnyOf(MockCIVisibilityTest targetTest, string key, params string[] values)
    {
        var actualValue = targetTest.Meta[key];
        values.Should().Contain(actualValue);
        targetTest.Meta.Remove(key);
        return actualValue;
    }

    protected virtual void AssertTargetSpanEqual(MockCIVisibilityTest targetTest, string key, string value)
    {
        targetTest.Meta[key].Should().Be(value);
        targetTest.Meta.Remove(key);
    }

    protected virtual void AssertTargetSpanExists(MockCIVisibilityTest targetTest, string key)
    {
        targetTest.Meta.Should().ContainKey(key);
        targetTest.Meta.Remove(key);
    }

    protected virtual void AssertTargetSpanContains(MockCIVisibilityTest targetTest, string key, string value)
    {
        targetTest.Meta[key].Should().Contain(value);
        targetTest.Meta.Remove(key);
    }

    protected virtual void CheckRuntimeValues(MockCIVisibilityTest targetTest)
    {
        AssertTargetSpanExists(targetTest, CommonTags.RuntimeName);
        AssertTargetSpanExists(targetTest, CommonTags.RuntimeVersion);
        AssertTargetSpanExists(targetTest, CommonTags.RuntimeArchitecture);
        AssertTargetSpanExists(targetTest, CommonTags.OSArchitecture);
        AssertTargetSpanExists(targetTest, CommonTags.OSPlatform);
        AssertTargetSpanEqual(targetTest, CommonTags.OSVersion, CIVisibility.GetOperatingSystemVersion());
    }

    protected virtual void CheckOriginTag(MockCIVisibilityTest targetTest)
    {
        // Check the test origin tag
        AssertTargetSpanEqual(targetTest, Tags.Origin, TestTags.CIAppTestOriginName);
    }

    protected virtual void CheckCIEnvironmentValuesDecoration(MockCIVisibilityTest targetTest, string? repository = null, string? branch = null, string? commitSha = null)
    {
        var context = new SpanContext(parent: null, traceContext: null, serviceName: null);
        var span = new Span(context, DateTimeOffset.UtcNow);
        ((CIEnvironmentValues?)CIValues)?.DecorateSpan(span);

        AssertEqual(CommonTags.CIProvider);
        AssertEqual(CommonTags.CIPipelineId);
        AssertEqual(CommonTags.CIPipelineName);
        AssertEqual(CommonTags.CIPipelineNumber);
        AssertEqual(CommonTags.CIPipelineUrl);
        AssertEqual(CommonTags.CIJobUrl);
        AssertEqual(CommonTags.CIJobName);
        AssertEqual(CommonTags.StageName);
        AssertEqual(CommonTags.CIWorkspacePath);
        AssertEqual(CommonTags.GitRepository, repository);
        AssertEqual(CommonTags.GitCommit, commitSha);
        AssertEqual(CommonTags.GitBranch, branch);
        AssertEqual(CommonTags.GitTag);
        AssertEqual(CommonTags.GitCommitAuthorName);
        AssertEqual(CommonTags.GitCommitAuthorEmail);
        AssertEqualDate(CommonTags.GitCommitAuthorDate);
        AssertEqual(CommonTags.GitCommitCommitterName);
        AssertEqual(CommonTags.GitCommitCommitterEmail);
        AssertEqualDate(CommonTags.GitCommitCommitterDate);
        AssertEqual(CommonTags.GitCommitMessage);
        AssertEqual(CommonTags.BuildSourceRoot);
        AssertEqual(CommonTags.CiEnvVars);

        void AssertEqual(string key, string? value = null)
        {
            if (value is null)
            {
                if (span.GetTag(key) is { } tagValue)
                {
                    targetTest.Meta[key].Should().Be(tagValue);
                    targetTest.Meta.Remove(key);
                }
            }
            else
            {
                targetTest.Meta[key].Should().Be(value);
                targetTest.Meta.Remove(key);
            }
        }

        void AssertEqualDate(string key)
        {
            if (span.GetTag(key) is { } keyValue)
            {
                DateTimeOffset.Parse(targetTest.Meta[key]).Should().Be(DateTimeOffset.Parse(keyValue));
                targetTest.Meta.Remove(key);
            }
        }
    }

    protected virtual void CheckTraitsValues(MockCIVisibilityTest targetTest)
    {
        // Check the traits tag value
        AssertTargetSpanEqual(targetTest, TestTags.Traits, "{\"Category\":[\"Category01\"],\"Compatibility\":[\"Windows\",\"Linux\"]}");
    }

    protected virtual void CheckSimpleTestSpan(MockCIVisibilityTest targetTest)
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusPass);
    }

    protected virtual void CheckSimpleSkipFromAttributeTest(MockCIVisibilityTest targetTest, string skipReason = "Simple skip reason")
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusSkip);

        // Check the Test skip reason
        AssertTargetSpanEqual(targetTest, TestTags.SkipReason, skipReason);
    }

    protected virtual void CheckSimpleErrorTest(MockCIVisibilityTest targetTest)
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetTest, TestTags.Status, TestTags.StatusFail);

        // Check the span error flag
        targetTest.Error.Should().Be(1);

        // Check the error type
        AssertTargetSpanEqual(targetTest, Tags.ErrorType, typeof(DivideByZeroException).FullName ?? nameof(DivideByZeroException));

        // Check the error stack
        AssertTargetSpanContains(targetTest, Tags.ErrorStack, "at");

        // Check the error message
        AssertTargetSpanEqual(targetTest, Tags.ErrorMsg, new DivideByZeroException().Message);
    }

    protected void SetCIEnvironmentValues()
    {
        var current = GitInfo.GetCurrent();
        var ciDictionaryValues = new Dictionary<string, string>
        {
            [CIEnvironmentValues.Constants.AzureTFBuild] = "1",
            [CIEnvironmentValues.Constants.AzureSystemTeamProjectId] = "TeamProjectId",
            [CIEnvironmentValues.Constants.AzureBuildBuildId] = "BuildId",
            [CIEnvironmentValues.Constants.AzureSystemJobId] = "JobId",
            [CIEnvironmentValues.Constants.AzureBuildSourcesDirectory] = current.SourceRoot,
            [CIEnvironmentValues.Constants.AzureBuildDefinitionName] = "DefinitionName",
            [CIEnvironmentValues.Constants.AzureSystemTeamFoundationServerUri] = "https://foundation.server.url/",
            [CIEnvironmentValues.Constants.AzureSystemStageDisplayName] = "StageDisplayName",
            [CIEnvironmentValues.Constants.AzureSystemJobDisplayName] = "JobDisplayName",
            [CIEnvironmentValues.Constants.AzureSystemTaskInstanceId] = "TaskInstanceId",
            [CIEnvironmentValues.Constants.AzureSystemPullRequestSourceRepositoryUri] = "git@github.com:DataDog/dd-trace-dotnet.git",
            [CIEnvironmentValues.Constants.AzureBuildRepositoryUri] = "git@github.com:DataDog/dd-trace-dotnet.git",
            [CIEnvironmentValues.Constants.AzureSystemPullRequestSourceCommitId] = "3245605c3d1edc67226d725799ee969c71f7632b",
            [CIEnvironmentValues.Constants.AzureBuildSourceVersion] = "3245605c3d1edc67226d725799ee969c71f7632b",
            [CIEnvironmentValues.Constants.AzureSystemPullRequestSourceBranch] = "main",
            [CIEnvironmentValues.Constants.AzureBuildSourceBranch] = "main",
            [CIEnvironmentValues.Constants.AzureBuildSourceBranchName] = "main",
            [CIEnvironmentValues.Constants.AzureBuildSourceVersionMessage] = "Fake commit for testing",
            [CIEnvironmentValues.Constants.AzureBuildRequestedForId] = "AuthorName",
            [CIEnvironmentValues.Constants.AzureBuildRequestedForEmail] = "author@company.com",
        };

        foreach (var item in ciDictionaryValues)
        {
            SetEnvironmentVariable(item.Key, item.Value);
        }

        CIValues = CIEnvironmentValues.Create(ciDictionaryValues);
    }
}
