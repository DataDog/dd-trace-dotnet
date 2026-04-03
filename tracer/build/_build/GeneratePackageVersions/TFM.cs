// <copyright file="TFM.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace GeneratePackageVersions;

/// <summary>
/// Short aliases for <see cref="TargetFramework"/> values, for use in
/// <see cref="IntegrationDefinitions"/>. Keeps the definitions compact and readable.
/// </summary>
public static class TFM
{
    public static readonly TargetFramework Net461 = TargetFramework.NET461;
    public static readonly TargetFramework Net48 = TargetFramework.NET48;
    public static readonly TargetFramework NetStandard20 = TargetFramework.NETSTANDARD2_0;
    public static readonly TargetFramework NetCoreApp21 = TargetFramework.NETCOREAPP2_1;
    public static readonly TargetFramework NetCoreApp30 = TargetFramework.NETCOREAPP3_0;
    public static readonly TargetFramework NetCoreApp31 = TargetFramework.NETCOREAPP3_1;
    public static readonly TargetFramework Net50 = TargetFramework.NET5_0;
    public static readonly TargetFramework Net60 = TargetFramework.NET6_0;
    public static readonly TargetFramework Net70 = TargetFramework.NET7_0;
    public static readonly TargetFramework Net80 = TargetFramework.NET8_0;
    public static readonly TargetFramework Net90 = TargetFramework.NET9_0;
    public static readonly TargetFramework Net100 = TargetFramework.NET10_0;

    /// <summary>
    /// The default set of frameworks supported by most sample projects
    /// (matches Directory.Build.props for test-applications).
    /// </summary>
    public static readonly TargetFramework[] Default =
    {
        Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31,
        Net50, Net60, Net70, Net80, Net90, Net100,
    };
}
