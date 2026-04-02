// <copyright file="VersionWithDate.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace GeneratePackageVersions;

/// <summary>
/// A NuGet package version paired with its publish date.
/// A null <see cref="Published"/> means the package predates NuGet tracking publish dates
/// and is treated as old enough to pass any cooldown check.
/// </summary>
public record VersionWithDate(string Version, DateTimeOffset? Published);
