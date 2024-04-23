// <copyright file="TestSessionSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Ci.Tagging;

internal partial class TestSessionSpanTags : Trace.Tagging.CommonTags
{
    public TestSessionSpanTags()
    {
        LibraryVersion = TracerConstants.AssemblyVersion;
    }

    public ulong SessionId { get; set; }

    [Tag(TestTags.Command)]
    public string Command { get; set; }

    [Tag(TestTags.CommandWorkingDirectory)]
    public string WorkingDirectory { get; set; }

    [Tag(TestTags.CommandExitCode)]
    public string CommandExitCode { get; set; }

    [Tag(TestTags.Status)]
    public string Status { get; set; }

    [Tag(CommonTags.LibraryVersion)]
    public string LibraryVersion { get; }

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

    [Tag(CommonTags.GitCommitAuthorDate)]
    public string GitCommitAuthorDate { get; set; }

    [Tag(CommonTags.GitCommitCommitterDate)]
    public string GitCommitCommitterDate { get; set; }

    [Tag(CommonTags.CiEnvVars)]
    public string CiEnvVars { get; set; }

    [Tag(IntelligentTestRunnerTags.TestsSkipped)]
    public string TestsSkipped { get; set; }

    [Tag(IntelligentTestRunnerTags.SkippingType)]
    public string IntelligentTestRunnerSkippingType { get; set; }

    [Tag(EarlyFlakeDetectionTags.Enabled)]
    public string EarlyFlakeDetectionTestEnabled { get; set; }

    [Tag(EarlyFlakeDetectionTags.AbortReason)]
    public string EarlyFlakeDetectionTestAbortReason { get; set; }

    public void SetCIEnvironmentValues(CIEnvironmentValues environmentValues)
    {
        if (environmentValues is not null)
        {
            CIProvider = environmentValues.Provider;
            CIPipelineId = environmentValues.PipelineId;
            CIPipelineName = environmentValues.PipelineName;
            CIPipelineNumber = environmentValues.PipelineNumber;
            CIPipelineUrl = environmentValues.PipelineUrl;
            CIJobName = environmentValues.JobName;
            CIJobUrl = environmentValues.JobUrl;
            StageName = environmentValues.StageName;
            CIWorkspacePath = environmentValues.WorkspacePath;
            GitRepository = environmentValues.Repository;
            GitCommit = environmentValues.Commit;
            GitBranch = environmentValues.Branch;
            GitTag = environmentValues.Tag;
            GitCommitAuthorDate = environmentValues.AuthorDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture);
            GitCommitAuthorName = environmentValues.AuthorName;
            GitCommitAuthorEmail = environmentValues.AuthorEmail;
            GitCommitCommitterDate = environmentValues.CommitterDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture);
            GitCommitCommitterName = environmentValues.CommitterName;
            GitCommitCommitterEmail = environmentValues.CommitterEmail;
            GitCommitMessage = environmentValues.Message;
            BuildSourceRoot = environmentValues.SourceRoot;

            if (environmentValues.VariablesToBypass is { } variablesToBypass)
            {
                CiEnvVars = Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(variablesToBypass);
            }
        }
    }
}
