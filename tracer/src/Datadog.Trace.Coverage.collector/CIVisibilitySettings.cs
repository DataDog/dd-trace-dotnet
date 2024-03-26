// <copyright file="CIVisibilitySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Ci.Configuration;

/// <summary>
/// A lightweight shim for creating the CI Visibility Settings
/// </summary>
internal partial class CIVisibilitySettings
{
    public CIVisibilitySettings(IConfigurationSource source)
    {
        var config = new ConfigurationBuilder(source, NullConfigurationTelemetry.Instance);
        CodeCoverageSnkFilePath = config.WithKeys(ConfigurationKeys.CIVisibility.CodeCoverageSnkFile).AsString();
        CodeCoverageEnableJitOptimizations = config.WithKeys(ConfigurationKeys.CIVisibility.CodeCoverageEnableJitOptimizations).AsBool(true);
        CodeCoverageMode = config.WithKeys(ConfigurationKeys.CIVisibility.CodeCoverageMode).AsString();
    }

    public static CIVisibilitySettings FromDefaultSources()
    {
        return new CIVisibilitySettings(GlobalConfigurationSource.Instance);
    }
}
