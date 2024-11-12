// <copyright file="ITelemeteredConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry;

/// <summary>
/// A version of <see cref="IConfigurationSource"/> that also allows reports the source of the telemetry
/// when a value is retrieved using a key.
/// </summary>
internal interface ITelemeteredConfigurationSource
{
    /// <summary>
    /// Gets whether the specified key is present in the source.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <returns><c>true</c> if the key is present in the source, false otherwise.</returns>
    bool IsPresent(string key);

    /// <summary>
    /// Gets the <see cref="string"/> value of
    /// the setting with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <param name="telemetry">The context for recording telemetry.</param>
    /// <param name="validator">An optional validation function that must be applied to
    /// a successfully extracted value to determine if it should be accepted</param>
    /// <param name="recordValue">If <c>true</c> the value should be recorded in telemetry. If not, the source value should be redacted</param>
    /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
    ConfigurationResult<string> GetString(
        string key,
        IConfigurationTelemetry telemetry,
        Func<string, bool>? validator,
        bool recordValue);

    /// <summary>
    /// Gets the <see cref="int"/> value of
    /// the setting with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <param name="telemetry">The context for recording telemetry.</param>
    /// <param name="validator">An optional validation function that must be applied to
    /// a successfully extracted value to determine if it should be accepted</param>
    /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
    ConfigurationResult<int> GetInt32(string key, IConfigurationTelemetry telemetry, Func<int, bool>? validator);

    /// <summary>
    /// Gets the <see cref="double"/> value of
    /// the setting with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <param name="telemetry">The context for recording telemetry.</param>
    /// <param name="validator">An optional validation function that must be applied to
    /// a successfully extracted value to determine if it should be accepted</param>
    /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
    ConfigurationResult<double> GetDouble(string key, IConfigurationTelemetry telemetry, Func<double, bool>? validator);

    /// <summary>
    /// Gets the <see cref="bool"/> value of
    /// the setting with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <param name="telemetry">The context for recording telemetry.</param>
    /// <param name="validator">An optional validation function that must be applied to
    /// a successfully extracted value to determine if it should be accepted</param>
    /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
    ConfigurationResult<bool> GetBool(string key, IConfigurationTelemetry telemetry, Func<bool, bool>? validator);

    /// <summary>
    /// Gets the <see cref="IDictionary{TKey, TValue}"/> value of
    /// the setting with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <param name="telemetry">The context for recording telemetry.</param>
    /// <param name="validator">An optional validation function that must be applied to
    /// a successfully extracted value to determine if it should be accepted</param>
    /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
    ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator);

    /// <summary>
    /// Gets the <see cref="IDictionary{TKey, TValue}"/> value of
    /// the setting with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <param name="telemetry">The context for recording telemetry.</param>
    /// <param name="validator">An optional validation function that must be applied to
    /// a successfully extracted value to determine if it should be accepted</param>
    /// <param name="allowOptionalMappings">Determines whether to create dictionary entries when the input has no value mapping</param>
    /// <param name="separator">Sets the character that separates keys and values in the input</param>
    /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
    ConfigurationResult<IDictionary<string, string>> GetDictionary(string key, IConfigurationTelemetry telemetry, Func<IDictionary<string, string>, bool>? validator, bool allowOptionalMappings, char separator);

    /// <summary>
    /// Gets the <see cref="IDictionary{TKey, TValue}"/> value of
    /// the setting with the specified key.
    /// </summary>
    /// <param name="key">The key that identifies the setting.</param>
    /// <param name="telemetry">The context for recording telemetry.</param>
    /// <param name="converter">A converter that parses the "raw" string configuration value into the expected value.</param>
    /// <param name="validator">An optional validation function that must be applied to
    /// a successfully extracted value to determine if it should be accepted</param>
    /// <param name="recordValue">If <c>true</c> the value should be recorded in telemetry. If not, the source value should be redacted</param>
    /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
    ConfigurationResult<T> GetAs<T>(
        string key,
        IConfigurationTelemetry telemetry,
        Func<string, ParsingResult<T>> converter,
        Func<T, bool>? validator,
        bool recordValue);
}
