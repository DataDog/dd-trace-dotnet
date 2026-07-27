// <copyright file="FeatureFlagSpanTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary>
    /// The encoded <c>ffe_*</c> tag values for a root span, produced by
    /// <see cref="SpanEnrichmentState.BuildSpanTags"/> on the serializer thread. A struct so the
    /// build path allocates nothing beyond the encoded strings themselves. Any field may be null
    /// when that tag has no data.
    /// </summary>
    internal readonly struct FeatureFlagSpanTags
    {
        public FeatureFlagSpanTags(string? flagsEnc, string? subjectsEnc, string? runtimeDefaults)
        {
            FlagsEnc = flagsEnc;
            SubjectsEnc = subjectsEnc;
            RuntimeDefaults = runtimeDefaults;
        }

        /// <summary>Gets the base64 ULEB128 delta-varint of the flag serial ids (<c>ffe_flags_enc</c>).</summary>
        public string? FlagsEnc { get; }

        /// <summary>Gets the JSON subjects map { sha256hex: base64 } (<c>ffe_subjects_enc</c>).</summary>
        public string? SubjectsEnc { get; }

        /// <summary>Gets the JSON runtime-defaults map { flagKey: valueStr } (<c>ffe_runtime_defaults</c>).</summary>
        public string? RuntimeDefaults { get; }

        /// <summary>Gets a value indicating whether any tag value is present.</summary>
        public bool HasAny =>
            !StringUtil.IsNullOrEmpty(FlagsEnc) ||
            !StringUtil.IsNullOrEmpty(SubjectsEnc) ||
            !StringUtil.IsNullOrEmpty(RuntimeDefaults);
    }
}
