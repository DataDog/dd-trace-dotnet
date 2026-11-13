// <copyright file="FeatureFlagMetadataKeys.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags
{
    internal static class FeatureFlagMetadataKeys
    {
        internal const string SplitSerialId = "__dd_split_serial_id";
        internal const string DoLog = "__dd_do_log";
        internal const string ExposureCacheHit = "__dd_exposure_cache_hit";
        internal const string AllocationKey = "__dd_allocation_key";
        internal const string ObserveFullEvaluationData = "__dd_observe_full_evaluation_data";
        internal const string EvaluationTimestampMs = "__dd_eval_timestamp_ms";
        internal const string PendingExposure = "__dd_pending_exposure";

        // One metadata entry is written for each feature of the selected split, per destination,
        // under that destination's prefix. Only the __dd_feature prefix is a public contract.
        internal const string FeaturePrefix = "__dd_feature.";
        internal const string ExposureFeaturePrefix = "__dd_exposure_feature.";
        internal const string EvaluationFeaturePrefix = "__dd_evaluation_feature.";

        // Feature values cross to the provider as strings. This entry lists the features whose value is a
        // boolean or a number, as "<key length>:<key><b|n>" items, so a string such as "true" stays a
        // string. It is written only when there is such a feature; its absence means every value is a string.
        internal const string FeatureTypes = "__dd_feature_types";
    }
}
