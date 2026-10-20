// <copyright file="ExperimentationOptOutMode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>Controls per-subject exclusion from experimentation.</summary>
public enum ExperimentationOptOutMode
{
    /// <summary>Uses DD_FEATURE_FLAGS_EXPERIMENTATION_OPT_OUT_MODE, which defaults to disabled.</summary>
    Default = 0,

    /// <summary>Ignores dd_experimentation_opt_out_at, overriding central configuration.</summary>
    Disabled = 1,

    /// <summary>Excludes subjects immediately when dd_experimentation_opt_out_at contains an RFC3339 timestamp.</summary>
    Immediate = 2,
}
