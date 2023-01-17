// <copyright file="TagsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;

namespace Datadog.Trace.Util;

/// <summary>
/// Taken from:
/// https://github.com/DataDog/dd-trace-java/blob/069ada67c23fd6735fb6722a8d57b9d3e40edc87/internal-api/src/main/java/datadog/trace/util/TagsHelper.java
/// which was originally taken from:
/// https://github.com/DataDog/logs-backend/blob/d0b3289ce2c63c1e8f961f03fc4e03318fb36b0f/processing/src/main/java/com/dd/logs/processing/common/Tags.java#L44
/// </summary>
internal sealed class TagsHelper
{
    private const int MaxLength = 200;

    /// <summary>
    /// Sanitizes a tag, more or less following (but not exactly) the recommended Datadog guidelines.
    ///
    /// See the exact guidelines:
    /// https://docs.datadoghq.com/getting_started/tagging/#tags-best-practices
    ///
    /// 1. Tags must start with a letter, and after that may contain: - Alphanumerics - Underscores
    /// - Minuses - Colons - Periods - Slashes Other special characters get converted to underscores.
    /// Note: A tag cannot end with a colon (e.g., tag:)
    ///
    /// 2. Tags can be up to 200 characters long and support unicode.
    ///
    /// 3. Tags are converted to lowercase.
    ///
    /// 4. A tag can have a value or a key:value syntax: For optimal functionality, we recommend
    /// constructing tags that use the key:value syntax. The key is always what precedes the first
    /// colon of the global tag definition, e.g.: - role:database:mysql is parsed as key:role ,
    /// value:database:mysql - role_database:mysql is parsed as key:role_database , value:mysql
    /// Examples of commonly used metric tag keys are env, instance, name, and role.
    ///
    /// 5. device, host, and source are reserved tag keys and cannot be specified in the standard
    /// way.
    ///
    /// 6. Tags shouldn't originate from unbounded sources, such as EPOCH timestamps or user IDs.
    /// These tags may impact platform performance and billing.
    ///
    /// Changes: we trim leading and trailing spaces.
    /// </summary>
    /// <param name="tag">The tag to sanitize.</param>
    /// <returns>A sanitized tag, or null if the provided tag was null.</returns>
    public static string? Sanitize(string? tag)
    {
        if (tag == null)
        {
            return null;
        }

        string lower = tag.ToLower(CultureInfo.InvariantCulture).Trim();
        int length = Math.Min(lower.Length, MaxLength);
        var sanitized = StringBuilderCache.Acquire(length);

        for (int i = 0; i < length; ++i)
        {
            char c = lower[i];
            sanitized.Append(IsValid(c) ? c : '_');
        }

        return StringBuilderCache.GetStringAndRelease(sanitized);

        bool IsValid(char c) => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or '/' or ':';
    }
}
