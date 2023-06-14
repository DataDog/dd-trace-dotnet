// <copyright file="ImmutableGCPFunctionSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Settings class for gathering metadata about the execution context in Google Cloud Functions
    /// References:
    /// https://cloud.google.com/functions/docs/configuring/env-var#runtime_environment_variables_set_automatically
    /// </summary>
    internal class ImmutableGCPFunctionSettings
    {
        internal static bool GetIsGCPFunction()
        {
            bool isDeprecatedGCPFunction = Environment.GetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedFunctionNameKey) != null && Environment.GetEnvironmentVariable(ConfigurationKeys.GCPFunction.DeprecatedProjectKey) != null;
            bool isNewerGCPFunction = Environment.GetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionNameKey) != null && Environment.GetEnvironmentVariable(ConfigurationKeys.GCPFunction.FunctionTargetKey) != null;

            return isDeprecatedGCPFunction || isNewerGCPFunction;
        }
    }
}
