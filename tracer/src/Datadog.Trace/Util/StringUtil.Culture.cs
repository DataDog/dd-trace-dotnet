// <copyright file="StringUtil.Culture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace - Putting this in system so the helpers are available everywhere
namespace System;

/// <summary>
/// Culture-invariant formatting helpers, so callers don't need to pass <see cref="CultureInfo.InvariantCulture"/>
/// at every call site. This file is also linked into projects that don't reference Datadog.Trace.
/// </summary>
internal static partial class StringUtil
{
    extension<T>(T value)
        where T : IFormattable
    {
        /// <summary>
        /// Formats the value using the invariant culture, i.e. <c>value.ToString(null, CultureInfo.InvariantCulture)</c>.
        /// </summary>
        /// <returns>The value formatted using the invariant culture</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToStringInvariant() => value.ToString(null, CultureInfo.InvariantCulture);

        /// <summary>
        /// Formats the value using the specified format and the invariant culture,
        /// i.e. <c>value.ToString(format, CultureInfo.InvariantCulture)</c>.
        /// </summary>
        /// <param name="format">The format to use</param>
        /// <returns>The value formatted using the invariant culture</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToStringInvariant(string? format) => value.ToString(format, CultureInfo.InvariantCulture);
    }
}
