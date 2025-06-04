// <copyright file="EarlyFlakeDetectionTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.Tags;

/// <summary>
/// Early Flake Detection span tags
/// </summary>
internal static class EarlyFlakeDetectionTags
{
    /// <summary>
    /// Early Flake Detection is enabled flag
    /// </summary>
    public const string Enabled = "test.early_flake.enabled";

    /// <summary>
    /// Early Flake Detection abort reason flag
    /// </summary>
    public const string AbortReason = "test.early_flake.abort_reason";
}
