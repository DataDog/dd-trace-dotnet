// <copyright file="ProviderEnvironmentConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.FeatureFlags.OpenFeature;

internal static class ProviderEnvironmentConfiguration
{
    // This package cannot reference tracer internals. A test checks this key against the generated registry constant.
    internal const string EvaluationTelemetryOptOutEnabled = "DD_FEATURE_FLAGS_EVALUATION_TELEMETRY_PER_SUBJECT_OPT_OUT_ENABLED";

    internal static bool IsEvaluationTelemetryOptOutEnabled()
    {
        try
        {
            var value = Environment.GetEnvironmentVariable(EvaluationTelemetryOptOutEnabled);
            return value is not null && BooleanConfigurationParser.Parse(value) == true;
        }
        catch
        {
            // Match EnvironmentConfigurationSource: inaccessible environment configuration uses the default.
            return false;
        }
    }
}
