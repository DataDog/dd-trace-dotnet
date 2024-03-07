// <copyright file="UnsupportedCIEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.Environment;

internal sealed class UnsupportedCIEnvironmentValues<TValueProvider>(TValueProvider valueProvider) : CIEnvironmentValues<TValueProvider>(valueProvider)
    where TValueProvider : struct, IValueProvider
{
    protected override void OnInitialize(GitInfo gitInfo)
    {
        Log.Information("CIEnvironmentValues: CI could not be detected, using the git folder: {GitFolder}", gitInfo.SourceRoot);
        Branch = gitInfo.Branch;
        Commit = gitInfo.Commit;
        Repository = gitInfo.Repository;
        SourceRoot = gitInfo.SourceRoot;
        WorkspacePath = gitInfo.SourceRoot;
    }
}
