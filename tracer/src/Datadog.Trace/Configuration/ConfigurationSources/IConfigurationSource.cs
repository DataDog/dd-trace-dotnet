// <copyright file="IConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A source of configuration settings, identifiable by a string key.
    /// </summary>
    public interface IConfigurationSource
    {
        /// <summary>
        /// Gets the <see cref="string"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        string? GetString(string key);

        /// <summary>
        /// Gets the <see cref="int"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        int? GetInt32(string key);

        /// <summary>
        /// Gets the <see cref="double"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        double? GetDouble(string key);

        /// <summary>
        /// Gets the <see cref="bool"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        bool? GetBool(string key);

        /// <summary>
        /// Gets the <see cref="IDictionary{TKey, TValue}"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        IDictionary<string, string>? GetDictionary(string key);

        /// <summary>
        /// Gets the <see cref="IDictionary{TKey, TValue}"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        [PublicApi]
        IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings);
    }
}
