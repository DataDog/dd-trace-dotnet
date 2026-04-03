// <copyright file="IntegrationDefinition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;

namespace GeneratePackageVersions;

/// <summary>
/// Defines a single integration's package version testing configuration.
/// Each definition maps to one NuGet package, one sample project, and one version range.
/// Split-range packages (e.g., GraphQL 4.x-6.x and 7.x-9.x) use separate definitions
/// with distinct IntegrationName values.
/// </summary>
public class IntegrationDefinition
{
    /// <summary>
    /// Unique name for this integration entry. Must be a valid C# identifier.
    /// Used as property names in test data and as keys in the cooldown baseline.
    /// </summary>
    public required string IntegrationName { get; init; }

    /// <summary>
    /// The sample project that exercises this integration (e.g., "Samples.Hangfire").
    /// </summary>
    public required string SampleProjectName { get; init; }

    /// <summary>
    /// The NuGet package to query for available versions (e.g., "Hangfire.Core").
    /// </summary>
    public required string NuGetPackageName { get; init; }

    /// <summary>
    /// Minimum version (inclusive) of the NuGet package to test.
    /// </summary>
    public required string MinVersion { get; init; }

    /// <summary>
    /// Maximum version (exclusive) of the NuGet package to test.
    /// </summary>
    public required string MaxVersionExclusive { get; init; }

    /// <summary>
    /// Glob patterns selecting specific versions to test (e.g., "1.7.*", "2.*.*").
    /// When empty, falls back to latest-per-major selection.
    /// </summary>
    public string[] SpecificVersions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The full set of target frameworks this integration's sample project supports.
    /// Declared here instead of read from MSBuild to avoid the cross-platform TFM loss bug
    /// and to eliminate the build dependency chain.
    /// </summary>
    public TargetFramework[] SupportedFrameworks { get; init; } = Array.Empty<TargetFramework>();

    /// <summary>
    /// Version-range-scoped constraints that narrow the set of supported frameworks
    /// or exclude specific platforms for a subset of versions.
    /// </summary>
    public VersionConstraint[] Constraints { get; init; } = Array.Empty<VersionConstraint>();

    /// <summary>
    /// Whether the sample project requires Docker dependencies to run.
    /// Defaults to <see cref="DockerDependencyType.None"/>.
    /// </summary>
    public DockerDependencyType RequiresDockerDependency { get; init; } = DockerDependencyType.None;

    /// <summary>
    /// Subfolder under test/test-applications/ where the sample project lives.
    /// Defaults to "integrations". Override for special locations (e.g., "azure-functions").
    /// </summary>
    public string TestFolder { get; init; } = "integrations";

    /// <summary>
    /// Returns whether <paramref name="version"/> falls within the given <paramref name="constraint"/>'s range.
    /// When the constraint's MinVersion/MaxVersionExclusive are null, falls back to this definition's bounds.
    /// </summary>
    public bool IsVersionInConstraintRange(Version version, VersionConstraint constraint)
    {
        var min = string.IsNullOrEmpty(constraint.MinVersion)
            ? new Version(MinVersion)
            : new Version(constraint.MinVersion);

        var max = string.IsNullOrEmpty(constraint.MaxVersionExclusive)
            ? new Version(MaxVersionExclusive)
            : new Version(constraint.MaxVersionExclusive);

        return version >= min && version < max;
    }

    /// <summary>
    /// Returns whether any constraint with <see cref="VersionConstraint.SkipAlpine"/> applies to this version.
    /// </summary>
    public bool ShouldSkipAlpine(Version version)
    {
        return Constraints.Any(c => c.SkipAlpine && IsVersionInConstraintRange(version, c));
    }

    /// <summary>
    /// Returns whether any constraint with <see cref="VersionConstraint.SkipArm64"/> applies to this version.
    /// </summary>
    public bool ShouldSkipArm64(Version version)
    {
        return Constraints.Any(c => c.SkipArm64 && IsVersionInConstraintRange(version, c));
    }
}

/// <summary>
/// A constraint that applies to a subset of versions within an integration's range.
/// When MinVersion/MaxVersionExclusive are null, they default to the parent definition's bounds.
/// </summary>
public record VersionConstraint
{
    /// <summary>
    /// Minimum version (inclusive) this constraint applies to.
    /// Null means the parent definition's MinVersion.
    /// </summary>
    public string? MinVersion { get; init; }

    /// <summary>
    /// Maximum version (exclusive) this constraint applies to.
    /// Null means the parent definition's MaxVersionExclusive.
    /// </summary>
    public string? MaxVersionExclusive { get; init; }

    /// <summary>
    /// Frameworks to exclude for versions in this range.
    /// </summary>
    public TargetFramework[] ExcludeFrameworks { get; init; } = Array.Empty<TargetFramework>();

    /// <summary>
    /// If non-empty, only these frameworks are allowed for versions in this range.
    /// </summary>
    public TargetFramework[] OnlyFrameworks { get; init; } = Array.Empty<TargetFramework>();

    /// <summary>
    /// Skip ARM64 builds for versions in this range.
    /// </summary>
    public bool SkipArm64 { get; init; }

    /// <summary>
    /// Skip Alpine builds for versions in this range.
    /// </summary>
    public bool SkipAlpine { get; init; }
}
