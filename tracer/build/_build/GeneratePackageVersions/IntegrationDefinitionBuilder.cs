// <copyright file="IntegrationDefinitionBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace GeneratePackageVersions;

/// <summary>
/// Fluent builder for <see cref="IntegrationDefinition"/>. Provides a compact syntax
/// for declaring integration test configurations in <see cref="IntegrationDefinitions"/>.
/// </summary>
public class IntegrationDefinitionBuilder
{
    private string _integrationName;
    private string _sampleProject;
    private string _nugetPackage;
    private string _minVersion;
    private string _maxVersionExclusive;
    private string[] _specificVersions = Array.Empty<string>();
    private TargetFramework[] _frameworks;
    private readonly List<VersionConstraint> _constraints = new();
    private DockerDependencyType _dockerDependency = DockerDependencyType.None;
    private string _testFolder = "integrations";

    private IntegrationDefinitionBuilder(string integrationName)
    {
        _integrationName = integrationName;
    }

    public static IntegrationDefinitionBuilder Create(string integrationName) => new(integrationName);

    public IntegrationDefinitionBuilder Sample(string projectName)
    {
        _sampleProject = projectName;
        return this;
    }

    public IntegrationDefinitionBuilder Package(string nugetPackageName)
    {
        _nugetPackage = nugetPackageName;
        return this;
    }

    public IntegrationDefinitionBuilder Versions(string min, string maxExclusive)
    {
        _minVersion = min;
        _maxVersionExclusive = maxExclusive;
        return this;
    }

    public IntegrationDefinitionBuilder Specific(params string[] globs)
    {
        _specificVersions = globs;
        return this;
    }

    public IntegrationDefinitionBuilder Frameworks(params TargetFramework[] tfms)
    {
        _frameworks = tfms;
        return this;
    }

    public IntegrationDefinitionBuilder DockerDependency(DockerDependencyType type)
    {
        _dockerDependency = type;
        return this;
    }

    public IntegrationDefinitionBuilder TestFolder(string folder)
    {
        _testFolder = folder;
        return this;
    }

    /// <summary>
    /// Add a version-range-scoped constraint. Both min and max are optional;
    /// when null they default to the definition's bounds at evaluation time.
    /// </summary>
    public IntegrationDefinitionBuilder When(
        string minVersion = null,
        string maxVersionExclusive = null,
        TargetFramework[] excludeFrameworks = null,
        TargetFramework[] onlyFrameworks = null,
        bool skipArm64 = false,
        bool skipAlpine = false)
    {
        _constraints.Add(new VersionConstraint
        {
            MinVersion = minVersion,
            MaxVersionExclusive = maxVersionExclusive,
            ExcludeFrameworks = excludeFrameworks ?? Array.Empty<TargetFramework>(),
            OnlyFrameworks = onlyFrameworks ?? Array.Empty<TargetFramework>(),
            SkipArm64 = skipArm64,
            SkipAlpine = skipAlpine,
        });
        return this;
    }

    public IntegrationDefinition Build()
    {
        if (string.IsNullOrEmpty(_integrationName))
        {
            throw new InvalidOperationException("IntegrationName is required");
        }

        if (string.IsNullOrEmpty(_sampleProject))
        {
            throw new InvalidOperationException($"SampleProjectName is required for {_integrationName}");
        }

        if (string.IsNullOrEmpty(_nugetPackage))
        {
            throw new InvalidOperationException($"NuGetPackageName is required for {_integrationName}");
        }

        if (string.IsNullOrEmpty(_minVersion) || string.IsNullOrEmpty(_maxVersionExclusive))
        {
            throw new InvalidOperationException($"Version range is required for {_integrationName}");
        }

        return new IntegrationDefinition
        {
            IntegrationName = _integrationName,
            SampleProjectName = _sampleProject,
            NuGetPackageName = _nugetPackage,
            MinVersion = _minVersion,
            MaxVersionExclusive = _maxVersionExclusive,
            SpecificVersions = _specificVersions,
            SupportedFrameworks = _frameworks ?? TFM.Default,
            Constraints = _constraints.ToArray(),
            RequiresDockerDependency = _dockerDependency,
            TestFolder = _testFolder,
        };
    }
}
