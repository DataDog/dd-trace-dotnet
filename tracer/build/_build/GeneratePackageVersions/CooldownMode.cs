// <copyright file="CooldownMode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace GeneratePackageVersions;

/// <summary>
/// Controls how cooldown filtering is applied to a package.
/// </summary>
public enum CooldownMode
{
    /// <summary>
    /// Normal cooldown: versions published within the cooldown window are dropped, unless they
    /// are at or below the highest version we already tested in a prior run for this entry's range
    /// (so cooldown never downgrades a version we already shipped against).
    /// </summary>
    Normal,

    /// <summary>
    /// Bypass the cooldown filter: every NuGet version in this entry's range is accepted,
    /// including those published within the cooldown window. Used for packages explicitly
    /// targeted via --IncludePackages.
    /// </summary>
    BypassCooldown,

    /// <summary>
    /// Re-emit the previous run's versions verbatim. The generators are not re-run against NuGet;
    /// the previous .g.cs file is parsed and its versions are fed back through Write(). Used for
    /// non-targeted packages when --IncludePackages or --ExcludePackages filters are active.
    /// </summary>
    Freeze,
}
