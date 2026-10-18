// <copyright file="DatadogProviderOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>Options copied when a Datadog provider is constructed.</summary>
public sealed class DatadogProviderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether boolean <c>true</c> at the evaluation-context key
    /// <c>dd_evaluation_telemetry_opt_out</c> suppresses new Datadog evaluation telemetry.
    /// If unset, uses <c>DD_FEATURE_FLAGS_EVALUATION_TELEMETRY_PER_SUBJECT_OPT_OUT_ENABLED</c> at construction.
    /// An explicit value overrides the environment. Disabled by default. Other context values preserve telemetry.
    /// Flag results, experiment exposures, application hooks, and previously recorded telemetry remain unchanged.
    /// The attribute remains available to targeting rules and exposure reporting.
    /// </summary>
    public bool? EnableEvaluationTelemetryOptOutParameter { get; set; }
}
