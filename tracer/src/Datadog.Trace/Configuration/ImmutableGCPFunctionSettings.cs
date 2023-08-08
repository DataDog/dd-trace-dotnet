// <copyright file="ImmutableGCPFunctionSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Settings class for gathering metadata about the execution context in Google Cloud Functions
    /// References:
    /// https://cloud.google.com/functions/docs/configuring/env-var#runtime_environment_variables_set_automatically
    /// </summary>
    internal class ImmutableGCPFunctionSettings
    {
        public ImmutableGCPFunctionSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);

            var deprecatedFunctionKey = config.WithKeys(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey).AsString();
            var deprecatedProjectKey = config.WithKeys(ConfigurationKeys.GCPFunction.DeprecatedProjectKey).AsString();
            IsDeprecatedFunction = deprecatedFunctionKey != null && deprecatedProjectKey != null;

            var functionNameKey = config.WithKeys(ConfigurationKeys.GCPFunction.FunctionNameKey).AsString();
            var functionTargetKey = config.WithKeys(ConfigurationKeys.GCPFunction.FunctionTargetKey).AsString();
            IsNewerFunction = functionNameKey != null && functionTargetKey != null;

            IsGCPFunction = IsDeprecatedFunction || IsNewerFunction;
        }

        public bool IsGCPFunction { get; }

        public bool IsDeprecatedFunction { get; }

        public bool IsNewerFunction { get; }
    }
}
