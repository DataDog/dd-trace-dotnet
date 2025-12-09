// <copyright file="TestSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Ci.Tags;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci.Tagging;

internal sealed partial class TestSpanTags : TestSuiteSpanTags
{
    public TestSpanTags()
    {
    }

    public TestSpanTags(TestSuiteSpanTags suiteTags, string testName)
    {
        Name = testName;
        SuiteId = suiteTags.SuiteId;
        Suite = suiteTags.Suite;
        Framework = suiteTags.Framework;
        Module = suiteTags.Module;
        Type = suiteTags.Type;
        FrameworkVersion = suiteTags.FrameworkVersion;
        GitBranch = suiteTags.GitBranch;
        GitCommit = suiteTags.GitCommit;
        GitRepository = suiteTags.GitRepository;
        GitTag = suiteTags.GitTag;
        ModuleId = suiteTags.ModuleId;
        SessionId = suiteTags.SessionId;
        RuntimeArchitecture = suiteTags.RuntimeArchitecture;
        RuntimeName = suiteTags.RuntimeName;
        RuntimeVersion = suiteTags.RuntimeVersion;
        StageName = suiteTags.StageName;
        BuildSourceRoot = suiteTags.BuildSourceRoot;
        CiEnvVars = suiteTags.CiEnvVars;
        CIProvider = suiteTags.CIProvider;
        GitCommitMessage = suiteTags.GitCommitMessage;
        OSArchitecture = suiteTags.OSArchitecture;
        OSPlatform = suiteTags.OSPlatform;
        OSVersion = suiteTags.OSVersion;
        CIJobName = suiteTags.CIJobName;
        CIJobUrl = suiteTags.CIJobUrl;
        CIPipelineId = suiteTags.CIPipelineId;
        CIPipelineName = suiteTags.CIPipelineName;
        CIPipelineNumber = suiteTags.CIPipelineNumber;
        CIPipelineUrl = suiteTags.CIPipelineUrl;
        CIWorkspacePath = suiteTags.CIWorkspacePath;
        GitCommitAuthorDate = suiteTags.GitCommitAuthorDate;
        GitCommitAuthorEmail = suiteTags.GitCommitAuthorEmail;
        GitCommitAuthorName = suiteTags.GitCommitAuthorName;
        GitCommitCommitterDate = suiteTags.GitCommitCommitterDate;
        GitCommitCommitterEmail = suiteTags.GitCommitCommitterEmail;
        GitCommitCommitterName = suiteTags.GitCommitCommitterName;
        Command = suiteTags.Command;
        WorkingDirectory = suiteTags.WorkingDirectory;
    }

    [Tag(TestTags.Name)]
    public string? Name { get; set; }

    [Tag(TestTags.Parameters)]
    public string? Parameters { get; set; }

    [Metric(TestTags.SourceStart)]
    public double? SourceStart { get; set; }

    [Metric(TestTags.SourceEnd)]
    public double? SourceEnd { get; set; }

    [Tag(TestTags.Traits)]
    public string? Traits { get; set; }

    [Tag(TestTags.SkipReason)]
    public string? SkipReason { get; set; }

    [Tag(IntelligentTestRunnerTags.SkippedBy)]
    public string? SkippedByIntelligentTestRunner { get; set; }

    [Tag(IntelligentTestRunnerTags.UnskippableTag)]
    public string? Unskippable { get; set; }

    [Tag(IntelligentTestRunnerTags.ForcedRunTag)]
    public string? ForcedRun { get; set; }

    [Tag(TestTags.TestIsNew)]
    public string? TestIsNew { get; set; }

    [Tag(TestTags.TestIsRetry)]
    public string? TestIsRetry { get; set; }

    [Tag(TestTags.TestRetryReason)]
    public string? TestRetryReason { get; set; }

    [Tag(BrowserTags.BrowserDriver)]
    public string? BrowserDriver { get; set; }

    [Tag(BrowserTags.BrowserDriverVersion)]
    public string? BrowserDriverVersion { get; set; }

    [Tag(BrowserTags.BrowserName)]
    public string? BrowserName { get; set; }

    [Tag(BrowserTags.BrowserVersion)]
    public string? BrowserVersion { get; set; }

    [Tag(BrowserTags.IsRumActive)]
    public string? IsRumActive { get; set; }

    [Tag(BrowserTags.IsModified)]
    public string? IsModified { get; set; }

    [Tag(TestTags.TestIsQuarantined)]
    public string? IsQuarantined { get; set; }

    [Tag(TestTags.TestIsDisabled)]
    public string? IsDisabled { get; set; }

    [Tag(TestTags.TestIsAttemptToFix)]
    public string? IsAttemptToFix { get; set; }

    [Tag(TestTags.TestHasFailedAllRetries)]
    public string? HasFailedAllRetries { get; set; }

    [Tag(TestTags.TestAttemptToFixPassed)]
    public string? AttemptToFixPassed { get; set; }

    [Tag(CapabilitiesTags.LibraryCapabilitiesTestImpactAnalysis)]
    public string? CapabilitiesTestImpactAnalysis { get; set; }

    [Tag(CapabilitiesTags.LibraryCapabilitiesEarlyFlakeDetection)]
    public string? CapabilitiesEarlyFlakeDetection { get; set; }

    [Tag(CapabilitiesTags.LibraryCapabilitiesAutoTestRetries)]
    public string? CapabilitiesAutoTestRetries { get; set; }

    [Tag(CapabilitiesTags.LibraryCapabilitiesTestManagementQuarantine)]
    public string? CapabilitiesTestManagementQuarantine { get; set; }

    [Tag(CapabilitiesTags.LibraryCapabilitiesTestManagementDisable)]
    public string? CapabilitiesTestManagementDisable { get; set; }

    [Tag(CapabilitiesTags.LibraryCapabilitiesTestManagementAttemptToFix)]
    public string? CapabilitiesTestManagementAttemptToFix { get; set; }
}
