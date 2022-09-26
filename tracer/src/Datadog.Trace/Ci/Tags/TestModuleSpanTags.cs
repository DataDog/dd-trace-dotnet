// <copyright file="TestModuleSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci.Tags;

internal partial class TestModuleSpanTags : Tagging.CommonTags
{
    public ulong ModuleId { get; set; }

    [Tag(TestTags.Type)]
    public string Type { get; set; }

    [Tag(TestTags.Module)]
    public string Module { get; set; }

    [Tag(TestTags.Bundle)]
    public string Bundle => Module;

    [Tag(TestTags.Framework)]
    public string Framework { get; set; }

    [Tag(TestTags.FrameworkVersion)]
    public string FrameworkVersion { get; set; }

    [Tag(CommonTags.CIProvider)]
    public string CIProvider { get; set; }

    [Tag(CommonTags.CIPipelineId)]
    public string CIPipelineId { get; set; }

    [Tag(CommonTags.CIPipelineName)]
    public string CIPipelineName { get; set; }

    [Tag(CommonTags.CIPipelineNumber)]
    public string CIPipelineNumber { get; set; }

    [Tag(CommonTags.CIPipelineUrl)]
    public string CIPipelineUrl { get; set; }

    [Tag(CommonTags.CIJobUrl)]
    public string CIJobUrl { get; set; }

    [Tag(CommonTags.CIJobName)]
    public string CIJobName { get; set; }

    [Tag(CommonTags.StageName)]
    public string StageName { get; set; }

    [Tag(CommonTags.CIWorkspacePath)]
    public string CIWorkspacePath { get; set; }

    [Tag(CommonTags.GitRepository)]
    public string GitRepository { get; set; }

    [Tag(CommonTags.GitCommit)]
    public string GitCommit { get; set; }

    [Tag(CommonTags.GitBranch)]
    public string GitBranch { get; set; }

    [Tag(CommonTags.GitTag)]
    public string GitTag { get; set; }

    [Tag(CommonTags.GitCommitAuthorName)]
    public string GitCommitAuthorName { get; set; }

    [Tag(CommonTags.GitCommitAuthorEmail)]
    public string GitCommitAuthorEmail { get; set; }

    [Tag(CommonTags.GitCommitCommitterName)]
    public string GitCommitCommitterName { get; set; }

    [Tag(CommonTags.GitCommitCommitterEmail)]
    public string GitCommitCommitterEmail { get; set; }

    [Tag(CommonTags.GitCommitMessage)]
    public string GitCommitMessage { get; set; }

    [Tag(CommonTags.BuildSourceRoot)]
    public string BuildSourceRoot { get; set; }

    [Tag(CommonTags.LibraryVersion)]
    public string LibraryVersion { get; set; }

    [Tag(CommonTags.RuntimeName)]
    public string RuntimeName { get; set; }

    [Tag(CommonTags.RuntimeVersion)]
    public string RuntimeVersion { get; set; }

    [Tag(CommonTags.RuntimeArchitecture)]
    public string RuntimeArchitecture { get; set; }

    [Tag(CommonTags.OSArchitecture)]
    public string OSArchitecture { get; set; }

    [Tag(CommonTags.OSPlatform)]
    public string OSPlatform { get; set; }

    [Tag(CommonTags.OSVersion)]
    public string OSVersion { get; set; }

    [Tag(CommonTags.GitCommitAuthorDate)]
    public string GitCommitAuthorDate { get; set; }

    [Tag(CommonTags.GitCommitCommitterDate)]
    public string GitCommitCommitterDate { get; set; }

    [Tag(CommonTags.CiEnvVars)]
    public string CiEnvVars { get; set; }

    [Tag(CommonTags.TestsSkipped)]
    public string TestsSkipped { get; set; }

    [Tag(TestTags.Status)]
    public string Status { get; set; }
}
