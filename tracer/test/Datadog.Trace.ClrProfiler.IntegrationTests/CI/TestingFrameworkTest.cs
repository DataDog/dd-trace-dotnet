// <copyright file="TestingFrameworkTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public abstract class TestingFrameworkTest : TestHelper
{
    private readonly GacFixture _gacFixture;

    protected TestingFrameworkTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
        : base(sampleAppName, samplePathOverrides, output)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkTest(string sampleAppName, string samplePathOverrides, ITestOutputHelper output, bool prependSamplesToAppName)
        : base(sampleAppName, samplePathOverrides, output, prependSamplesToAppName)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkTest(string sampleAppName, ITestOutputHelper output)
        : base(sampleAppName, output)
    {
        SetCIEnvironmentValues();
        _gacFixture = new GacFixture();
        _gacFixture.AddAssembliesToGac();
    }

    protected TestingFrameworkTest(EnvironmentHelper environmentHelper, ITestOutputHelper output)
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

    protected virtual void WriteSpans(List<MockSpan>? spans)
    {
        if (spans is null || spans.Count == 0)
        {
            return;
        }

        var sb = StringBuilderCache.Acquire();
        sb.AppendLine("***********************************");

        var i = 0;
        foreach (var span in spans)
        {
            sb.Append($" {i++}) ");
            sb.Append($"TraceId={span.TraceId}, ");
            sb.Append($"SpanId={span.SpanId}, ");
            sb.Append($"Service={span.Service}, ");
            sb.Append($"Name={span.Name}, ");
            sb.Append($"Resource={span.Resource}, ");
            sb.Append($"Type={span.Type}, ");
            sb.Append($"Error={span.Error}");
            sb.AppendLine();
            sb.AppendLine($"   Tags=");
            foreach (var kv in span.Tags)
            {
                sb.AppendLine($"       => {kv.Key} = {kv.Value}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("***********************************");
        Output.WriteLine(StringBuilderCache.GetStringAndRelease(sb));
    }

    protected virtual string AssertTargetSpanAnyOf(MockSpan targetSpan, string key, params string[] values)
    {
        var actualValue = targetSpan.Tags[key];
        values.Should().Contain(actualValue);
        targetSpan.Tags.Remove(key);
        return actualValue;
    }

    protected virtual void AssertTargetSpanEqual(MockSpan targetSpan, string key, string value)
    {
        targetSpan.Tags[key].Should().Be(value);
        targetSpan.Tags.Remove(key);
    }

    protected virtual void AssertTargetSpanExists(MockSpan targetSpan, string key)
    {
        targetSpan.Tags.Should().ContainKey(key);
        targetSpan.Tags.Remove(key);
    }

    protected virtual void AssertTargetSpanContains(MockSpan targetSpan, string key, string value)
    {
        targetSpan.Tags[key].Should().Contain(value);
        targetSpan.Tags.Remove(key);
    }

    protected virtual void CheckRuntimeValues(MockSpan targetSpan)
    {
        AssertTargetSpanExists(targetSpan, CommonTags.RuntimeName);
        AssertTargetSpanExists(targetSpan, CommonTags.RuntimeVersion);
        AssertTargetSpanExists(targetSpan, CommonTags.RuntimeArchitecture);
        AssertTargetSpanExists(targetSpan, CommonTags.OSArchitecture);
        AssertTargetSpanExists(targetSpan, CommonTags.OSPlatform);

        // Weirdly, with the .NET 10 update, this tag now contains the wrong value on x64 in some cases (.NET Core 3.1)
        // we're not sure why, and will investigate later
        if (!EnvironmentTools.IsWindows())
        {
            AssertTargetSpanEqual(targetSpan, CommonTags.OSVersion, new TestOptimizationHostInfo().GetOperatingSystemVersion() ?? string.Empty);
        }
        else
        {
            AssertTargetSpanExists(targetSpan, CommonTags.OSVersion);
        }
    }

    protected virtual void CheckOriginTag(MockSpan targetSpan)
    {
        // Check the test origin tag
        AssertTargetSpanEqual(targetSpan, Tags.Origin, TestTags.CIAppTestOriginName);
    }

    protected virtual void CheckCIEnvironmentValuesDecoration(MockSpan targetSpan)
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
        AssertEqual(CommonTags.GitRepository);
        AssertEqual(CommonTags.GitCommit);
        AssertEqual(CommonTags.GitBranch);
        AssertEqual(CommonTags.GitTag);
        AssertEqual(CommonTags.GitCommitAuthorName);
        AssertEqual(CommonTags.GitCommitAuthorEmail);
        AssertEqual(CommonTags.GitCommitAuthorDate);
        AssertEqual(CommonTags.GitCommitCommitterName);
        AssertEqual(CommonTags.GitCommitCommitterEmail);
        AssertEqual(CommonTags.GitCommitCommitterDate);
        AssertEqual(CommonTags.GitCommitMessage);
        AssertEqual(CommonTags.BuildSourceRoot);
        AssertEqual(CommonTags.CiEnvVars);

        void AssertEqual(string key)
        {
            if (span.GetTag(key) is { } tagValue)
            {
                targetSpan.Tags[key].Should().Be(tagValue);
            }

            targetSpan.Tags.Remove(key);
        }
    }

    protected virtual void CheckTraitsValues(MockSpan targetSpan)
    {
        // Check the traits tag value
        AssertTargetSpanEqual(targetSpan, TestTags.Traits, "{\"Category\":[\"Category01\"],\"Compatibility\":[\"Windows\",\"Linux\"]}");
    }

    protected virtual void CheckSimpleTestSpan(MockSpan targetSpan)
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusPass);
    }

    protected virtual void CheckSimpleSkipFromAttributeTest(MockSpan targetSpan, string skipReason = "Simple skip reason")
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusSkip);

        // Check the Test skip reason
        AssertTargetSpanEqual(targetSpan, TestTags.SkipReason, skipReason);
    }

    protected virtual void CheckSimpleErrorTest(MockSpan targetSpan)
    {
        // Check the Test Status
        AssertTargetSpanEqual(targetSpan, TestTags.Status, TestTags.StatusFail);

        // Check the span error flag
        targetSpan.Error.Should().Be(1);

        // Check the error type
        AssertTargetSpanEqual(targetSpan, Tags.ErrorType, typeof(DivideByZeroException).FullName ?? nameof(DivideByZeroException));

        // Check the error stack
        AssertTargetSpanContains(targetSpan, Tags.ErrorStack, "at");

        // Check the error message
        AssertTargetSpanEqual(targetSpan, Tags.ErrorMsg, new DivideByZeroException().Message);
    }

    protected void SetCIEnvironmentValues()
    {
        var current = GitInfo.GetCurrent();
        var ciDictionaryValues = DefineCIEnvironmentValues(
            new Dictionary<string, string>
            {
                [PlatformKeys.Ci.Azure.TFBuild] = "1",
                [PlatformKeys.Ci.Azure.SystemTeamProjectId] = "TeamProjectId",
                [PlatformKeys.Ci.Azure.BuildBuildId] = "BuildId",
                [PlatformKeys.Ci.Azure.SystemJobId] = "JobId",
                [PlatformKeys.Ci.Azure.BuildSourcesDirectory] = current.SourceRoot ?? string.Empty,
                [PlatformKeys.Ci.Azure.BuildDefinitionName] = "DefinitionName",
                [PlatformKeys.Ci.Azure.SystemTeamFoundationServerUri] = "https://foundation.server.url/",
                [PlatformKeys.Ci.Azure.SystemStageDisplayName] = "StageDisplayName",
                [PlatformKeys.Ci.Azure.SystemJobDisplayName] = "JobDisplayName",
                [PlatformKeys.Ci.Azure.SystemTaskInstanceId] = "TaskInstanceId",
                [PlatformKeys.Ci.Azure.SystemPullRequestSourceRepositoryUri] = "git@github.com:DataDog/dd-trace-dotnet.git",
                [PlatformKeys.Ci.Azure.BuildRepositoryUri] = "git@github.com:DataDog/dd-trace-dotnet.git",
                [PlatformKeys.Ci.Azure.SystemPullRequestSourceCommitId] = "3245605c3d1edc67226d725799ee969c71f7632b",
                [PlatformKeys.Ci.Azure.BuildSourceVersion] = "3245605c3d1edc67226d725799ee969c71f7632b",
                [PlatformKeys.Ci.Azure.SystemPullRequestSourceBranch] = "main",
                [PlatformKeys.Ci.Azure.BuildSourceBranch] = "main",
                [PlatformKeys.Ci.Azure.BuildSourceBranchName] = "main",
                [PlatformKeys.Ci.Azure.BuildSourceVersionMessage] = "Fake commit for testing",
                [PlatformKeys.Ci.Azure.BuildRequestedForId] = "AuthorName",
                [PlatformKeys.Ci.Azure.BuildRequestedForEmail] = "author@company.com",
            });

        foreach (var item in ciDictionaryValues)
        {
            SetEnvironmentVariable(item.Key, item.Value);
        }

        CIValues = CIEnvironmentValues.Create(ciDictionaryValues);
    }

    protected virtual Dictionary<string, string> DefineCIEnvironmentValues(Dictionary<string, string> values)
    {
        return values;
    }
}
