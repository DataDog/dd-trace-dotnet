// <copyright file="FlagSelection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// Selects the flags a provider loads: a flag is loaded when it has any of the <see cref="Tags"/> or its
/// key matches any of the <see cref="KeyPatterns"/>. Datadog filters the flag configuration before
/// sending it, so evaluating a flag outside the selection returns the default value with
/// <c>FLAG_NOT_FOUND</c>. Needs a tracer version that supports flag selection; with an older tracer,
/// every flag loads.
/// </summary>
public sealed class FlagSelection
{
    /// <summary>
    /// Gets or sets the tags to select. Matching is exact and case-sensitive. At most 31 tags of at most
    /// 200 UTF-8 bytes each, after surrounding white space is trimmed; otherwise the selection is not
    /// sent and every flag loads.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets the key patterns to select. A pattern matches the whole flag key, case-sensitively,
    /// and <c>*</c> matches any run of characters; every other character is literal. At most 31 patterns
    /// of at most 200 UTF-8 bytes each, after surrounding white space is trimmed; otherwise the selection
    /// is not sent and every flag loads.
    /// </summary>
    public IReadOnlyList<string>? KeyPatterns { get; set; }
}
