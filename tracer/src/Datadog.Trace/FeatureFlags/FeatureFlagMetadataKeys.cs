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
    }
}
