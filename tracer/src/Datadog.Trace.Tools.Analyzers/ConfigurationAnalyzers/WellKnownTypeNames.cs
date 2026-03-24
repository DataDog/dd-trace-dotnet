// <copyright file="WellKnownTypeNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Analyzers.ConfigurationAnalyzers
{
    /// <summary>
    /// Well-known type names and method names used across configuration analyzers.
    /// </summary>
    internal static class WellKnownTypeNames
    {
        // Method names
        public const string WithKeysMethodName = "WithKeys";

        // Configuration types
        public const string ConfigurationKeys = "Datadog.Trace.Configuration.ConfigurationKeys";
        public const string PlatformKeys = "Datadog.Trace.Configuration.PlatformKeys";
        public const string IConfigurationSource = "Datadog.Trace.Configuration.IConfigurationSource";

        // Configuration telemetry types
        public const string ConfigurationBuilder = "Datadog.Trace.Configuration.Telemetry.ConfigurationBuilder";
        public const string HasKeys = "Datadog.Trace.Configuration.Telemetry.HasKeys";

        // Utility types
        public const string EnvironmentHelpers = "Datadog.Trace.Util.EnvironmentHelpers";
        public const string EnvironmentHelpersNoLogging = "Datadog.Trace.Util.EnvironmentHelpersNoLogging";

        // CI types
        public const string IValueProvider = "Datadog.Trace.Ci.CiEnvironment.IValueProvider";
    }
}
