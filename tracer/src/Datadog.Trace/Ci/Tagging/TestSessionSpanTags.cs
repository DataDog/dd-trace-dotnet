// <copyright file="TestSessionSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Globalization;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.Ci.Tagging;

internal partial class TestSessionSpanTags : Trace.Tagging.TagsList
{
    public TestSessionSpanTags()
    {
        LibraryVersion = TracerConstants.AssemblyVersion;

        // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/environment-processorcount-on-windows#change-description
        LogicalCpuCount = Environment.ProcessorCount;
    }

    public ulong SessionId { get; set; }

    [Tag(TestTags.Command)]
    public string? Command { get; set; }

    [Tag(TestTags.CommandWorkingDirectory)]
    public string? WorkingDirectory { get; set; }

    [Tag(TestTags.CommandExitCode)]
    public string? CommandExitCode { get; set; }

    [Tag(TestTags.Status)]
    public string? Status { get; set; }

    [Tag(CommonTags.LibraryVersion)]
    public string? LibraryVersion { get; }

    [Tag(CommonTags.CIProvider)]
    public string? CIProvider { get; set; }

    [Tag(CommonTags.CIPipelineId)]
    public string? CIPipelineId { get; set; }

    [Tag(CommonTags.CIPipelineName)]
    public string? CIPipelineName { get; set; }

    [Tag(CommonTags.CIPipelineNumber)]
    public string? CIPipelineNumber { get; set; }

    [Tag(CommonTags.CIPipelineUrl)]
    public string? CIPipelineUrl { get; set; }

    [Tag(CommonTags.CIJobUrl)]
    public string? CIJobUrl { get; set; }

    [Tag(CommonTags.CIJobName)]
    public string? CIJobName { get; set; }

    [Tag(CommonTags.CIJobId)]
    public string? CIJobId { get; set; }

    [Tag(CommonTags.StageName)]
    public string? StageName { get; set; }

    [Tag(CommonTags.CIWorkspacePath)]
    public string? CIWorkspacePath { get; set; }

    [Tag(CommonTags.GitRepository)]
    public string? GitRepository { get; set; }

    [Tag(CommonTags.GitCommit)]
    public string? GitCommit { get; set; }

    [Tag(CommonTags.GitBranch)]
    public string? GitBranch { get; set; }

    [Tag(CommonTags.GitTag)]
    public string? GitTag { get; set; }

    [Tag(CommonTags.GitCommitAuthorName)]
    public string? GitCommitAuthorName { get; set; }

    [Tag(CommonTags.GitCommitAuthorEmail)]
    public string? GitCommitAuthorEmail { get; set; }

    [Tag(CommonTags.GitCommitCommitterName)]
    public string? GitCommitCommitterName { get; set; }

    [Tag(CommonTags.GitCommitCommitterEmail)]
    public string? GitCommitCommitterEmail { get; set; }

    [Tag(CommonTags.GitCommitMessage)]
    public string? GitCommitMessage { get; set; }

    [Tag(CommonTags.BuildSourceRoot)]
    public string? BuildSourceRoot { get; set; }

    [Tag(CommonTags.GitCommitAuthorDate)]
    public string? GitCommitAuthorDate { get; set; }

    [Tag(CommonTags.GitCommitCommitterDate)]
    public string? GitCommitCommitterDate { get; set; }

    [Tag(CommonTags.CiEnvVars)]
    public string? CiEnvVars { get; set; }

    [Tag(IntelligentTestRunnerTags.TestsSkipped)]
    public string? TestsSkipped { get; set; }

    [Tag(IntelligentTestRunnerTags.SkippingType)]
    public string? IntelligentTestRunnerSkippingType { get; set; }

    [Tag(EarlyFlakeDetectionTags.Enabled)]
    public string? EarlyFlakeDetectionTestEnabled { get; set; }

    [Tag(EarlyFlakeDetectionTags.AbortReason)]
    public string? EarlyFlakeDetectionTestAbortReason { get; set; }

    [Metric(CommonTags.LogicalCpuCount)]
    public double? LogicalCpuCount { get; }

    [Tag(CommonTags.GitPrBaseHeadCommit)]
    public string? GitPrBaseHeadCommit { get; set; }

    [Tag(CommonTags.GitPrBaseCommit)]
    public string? GitPrBaseCommit { get; set; }

    [Tag(CommonTags.GitPrBaseBranch)]
    public string? GitPrBaseBranch { get; set; }

    [Tag(CommonTags.PrNumber)]
    public string? PrNumber { get; set; }

    [Tag(CommonTags.GitHeadCommit)]
    public string? GitHeadCommit { get; set; }

    [Tag(CommonTags.GitHeadCommitAuthorName)]
    public string? GitHeadCommitAuthorName { get; set; }

    [Tag(CommonTags.GitHeadCommitAuthorEmail)]
    public string? GitHeadCommitAuthorEmail { get; set; }

    [Tag(CommonTags.GitHeadCommitAuthorDate)]
    public string? GitHeadCommitAuthorDate { get; set; }

    [Tag(CommonTags.GitHeadCommitCommitterName)]
    public string? GitHeadCommitCommitterName { get; set; }

    [Tag(CommonTags.GitHeadCommitCommitterEmail)]
    public string? GitHeadCommitCommitterEmail { get; set; }

    [Tag(CommonTags.GitHeadCommitCommitterDate)]
    public string? GitHeadCommitCommitterDate { get; set; }

    [Tag(CommonTags.GitHeadCommitMessage)]
    public string? GitHeadCommitMessage { get; set; }

    public void SetCIEnvironmentValues(CIEnvironmentValues environmentValues)
    {
        if (environmentValues is not null)
        {
            CIProvider = environmentValues.Provider;
            CIPipelineId = environmentValues.PipelineId;
            CIPipelineName = environmentValues.PipelineName;
            CIPipelineNumber = environmentValues.PipelineNumber;
            CIPipelineUrl = environmentValues.PipelineUrl;
            CIJobId = environmentValues.JobId;
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
            GitPrBaseHeadCommit = environmentValues.PrBaseHeadCommit;
            GitPrBaseCommit = environmentValues.PrBaseCommit;
            GitPrBaseBranch = environmentValues.PrBaseBranch;
            PrNumber = environmentValues.PrNumber;
            GitHeadCommit = environmentValues.HeadCommit;
            GitHeadCommitAuthorDate = environmentValues.HeadAuthorDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture);
            GitHeadCommitAuthorName = environmentValues.HeadAuthorName;
            GitHeadCommitAuthorEmail = environmentValues.HeadAuthorEmail;
            GitHeadCommitCommitterDate = environmentValues.HeadCommitterDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture);
            GitHeadCommitCommitterName = environmentValues.HeadCommitterName;
            GitHeadCommitCommitterEmail = environmentValues.HeadCommitterEmail;
            GitHeadCommitMessage = environmentValues.HeadMessage;

            if (environmentValues.VariablesToBypass is { } variablesToBypass)
            {
                CiEnvVars = JsonHelper.SerializeObject(variablesToBypass);
            }
        }
    }
}
