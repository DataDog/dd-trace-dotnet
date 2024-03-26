// <copyright file="TestSuiteSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Tags;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci.Tagging;

internal partial class TestSuiteSpanTags : TestModuleSpanTags
{
    public TestSuiteSpanTags()
    {
    }

    public TestSuiteSpanTags(TestModuleSpanTags moduleTags, string suiteName)
    {
        Suite = suiteName;
        Framework = moduleTags.Framework;
        Module = moduleTags.Module;
        Type = moduleTags.Type;
        FrameworkVersion = moduleTags.FrameworkVersion;
        GitBranch = moduleTags.GitBranch;
        GitCommit = moduleTags.GitCommit;
        GitRepository = moduleTags.GitRepository;
        GitTag = moduleTags.GitTag;
        ModuleId = moduleTags.ModuleId;
        SessionId = moduleTags.SessionId;
        RuntimeArchitecture = moduleTags.RuntimeArchitecture;
        RuntimeName = moduleTags.RuntimeName;
        RuntimeVersion = moduleTags.RuntimeVersion;
        StageName = moduleTags.StageName;
        BuildSourceRoot = moduleTags.BuildSourceRoot;
        CiEnvVars = moduleTags.CiEnvVars;
        CIProvider = moduleTags.CIProvider;
        GitCommitMessage = moduleTags.GitCommitMessage;
        OSArchitecture = moduleTags.OSArchitecture;
        OSPlatform = moduleTags.OSPlatform;
        OSVersion = moduleTags.OSVersion;
        SamplingLimitDecision = moduleTags.SamplingLimitDecision;
        TracesKeepRate = moduleTags.TracesKeepRate;
        CIJobName = moduleTags.CIJobName;
        CIJobUrl = moduleTags.CIJobUrl;
        CIPipelineId = moduleTags.CIPipelineId;
        CIPipelineName = moduleTags.CIPipelineName;
        CIPipelineNumber = moduleTags.CIPipelineNumber;
        CIPipelineUrl = moduleTags.CIPipelineUrl;
        CIWorkspacePath = moduleTags.CIWorkspacePath;
        GitCommitAuthorDate = moduleTags.GitCommitAuthorDate;
        GitCommitAuthorEmail = moduleTags.GitCommitAuthorEmail;
        GitCommitAuthorName = moduleTags.GitCommitAuthorName;
        GitCommitCommitterDate = moduleTags.GitCommitCommitterDate;
        GitCommitCommitterEmail = moduleTags.GitCommitCommitterEmail;
        GitCommitCommitterName = moduleTags.GitCommitCommitterName;
        Command = moduleTags.Command;
        WorkingDirectory = moduleTags.WorkingDirectory;
    }

    public ulong SuiteId { get; set; }

    [Tag(TestTags.Suite)]
    public string Suite { get; set; }
}
