// <copyright file="SkippableTestsRequestScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

/// <summary>
/// Describes the backend skippable-tests scope that must also be used for cache and coverage-backfill persistence.
/// </summary>
internal readonly struct SkippableTestsRequestScope : IEquatable<SkippableTestsRequestScope>
{
    /// <summary>
    /// Test granularity used by .NET test framework integrations when applying ITR skip decisions.
    /// </summary>
    public const string TestLevel = "test";

    /// <summary>
    /// Initializes a new instance of the <see cref="SkippableTestsRequestScope"/> struct.
    /// </summary>
    /// <param name="testBundle">Test bundle sent as `configurations["test.bundle"]`.</param>
    /// <param name="fingerprint">Stable scope fingerprint used for file cache and backfill data.</param>
    internal SkippableTestsRequestScope(string? testBundle, string? fingerprint)
    {
        TestBundle = string.IsNullOrWhiteSpace(testBundle) ? null : testBundle;
        Fingerprint = fingerprint ?? string.Empty;
    }

    /// <summary>
    /// Gets the test bundle sent to the skippable-tests endpoint, when the request is module scoped.
    /// </summary>
    public string? TestBundle { get; }

    /// <summary>
    /// Gets a stable fingerprint that includes every local dimension needed to safely reuse coverage backfill data.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>
    /// Gets a value indicating whether the scope carries a concrete test bundle.
    /// </summary>
    public bool HasTestBundle => !StringUtil.IsNullOrEmpty(TestBundle);

    /// <summary>
    /// Gets a value indicating whether the scope has a stable persistence/cache fingerprint.
    /// </summary>
    public bool HasFingerprint => !StringUtil.IsNullOrEmpty(Fingerprint);

    /// <summary>
    /// Creates a scope for the current process, including runtime and configuration dimensions from the active testhost.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance.</param>
    /// <param name="testBundle">Test bundle to request, or null for the legacy global scope.</param>
    /// <returns>A scope that can be used for backend requests and local cache keys.</returns>
    public static SkippableTestsRequestScope Create(ITestOptimization testOptimization, string? testBundle)
    {
        if (string.IsNullOrWhiteSpace(testBundle))
        {
            return default;
        }

        var fingerprint = BuildFingerprint(testOptimization, testBundle!);
        return new SkippableTestsRequestScope(testBundle, fingerprint);
    }

    /// <inheritdoc />
    public bool Equals(SkippableTestsRequestScope other)
        => string.Equals(TestBundle, other.TestBundle, StringComparison.Ordinal) &&
           string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is SkippableTestsRequestScope other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(TestBundle, Fingerprint);

    /// <summary>
    /// Builds the cache and persistence key from every local dimension that can change the backend coverage aggregate.
    /// </summary>
    /// <param name="testOptimization">Current Test Optimization instance.</param>
    /// <param name="testBundle">Test bundle that scopes the skippable-tests request.</param>
    /// <returns>Stable hexadecimal fingerprint for this request scope.</returns>
    private static string BuildFingerprint(ITestOptimization testOptimization, string testBundle)
    {
        var settings = testOptimization.Settings.TracerSettings.Manager.InitialMutableSettings;
        var framework = FrameworkDescription.Instance;
        var ciValues = testOptimization.CIValues;
        var builder = new StringBuilder();
        Append(builder, "v", "2");
        Append(builder, "service", NormalizerTraceProcessor.NormalizeService(settings.ServiceName) ?? string.Empty);
        Append(builder, "env", TraceUtil.NormalizeTag(settings.Environment ?? "none") ?? "none");
        Append(builder, "repository_url", ciValues.Repository ?? string.Empty);
        Append(builder, "sha", ciValues.Commit ?? string.Empty);
        Append(builder, "test_level", TestLevel);
        Append(builder, Tags.TestTags.Bundle, testBundle);
        Append(builder, Tags.CommonTags.OSPlatform, framework.OSPlatform);
        Append(builder, Tags.CommonTags.OSVersion, testOptimization.HostInfo.GetOperatingSystemVersion());
        Append(builder, Tags.CommonTags.OSArchitecture, framework.OSArchitecture);
        Append(builder, Tags.CommonTags.RuntimeName, framework.Name);
        Append(builder, Tags.CommonTags.RuntimeVersion, framework.ProductVersion);
        Append(builder, Tags.CommonTags.RuntimeArchitecture, framework.ProcessArchitecture);
        AppendCustomTestConfigurations(builder, settings.GlobalTags);

        var payload = Encoding.UTF8.GetBytes(builder.ToString());
#if NET6_0_OR_GREATER
        var bytes = SHA256.HashData(payload);
#else
        using HashAlgorithm sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(payload);
#endif
        return HexString.ToHexString(bytes);
    }

    /// <summary>
    /// Adds custom `test.configuration.*` tags in deterministic order so equivalent configuration sets hash identically.
    /// </summary>
    /// <param name="builder">Fingerprint payload builder.</param>
    /// <param name="globalTags">Tracer global tags that may contain custom test configurations.</param>
    private static void AppendCustomTestConfigurations(StringBuilder builder, IReadOnlyDictionary<string, string> globalTags)
    {
        const string testConfigurationPrefix = "test.configuration.";
        List<KeyValuePair<string, string>>? configurations = null;
        foreach (var tag in globalTags)
        {
            if (!tag.Key.StartsWith(testConfigurationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = tag.Key.Substring(testConfigurationPrefix.Length);
            if (StringUtil.IsNullOrEmpty(key))
            {
                continue;
            }

            configurations ??= [];
            configurations.Add(new KeyValuePair<string, string>(key, tag.Value));
        }

        if (configurations is null)
        {
            return;
        }

        configurations.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        foreach (var item in configurations)
        {
            Append(builder, $"custom.{item.Key}", item.Value);
        }
    }

    /// <summary>
    /// Appends one length-delimited fingerprint component to avoid collisions between adjacent values.
    /// </summary>
    /// <param name="builder">Fingerprint payload builder.</param>
    /// <param name="key">Scope dimension name.</param>
    /// <param name="value">Scope dimension value.</param>
    private static void Append(StringBuilder builder, string key, string value)
    {
        builder.Append(key.Length);
        builder.Append(':');
        builder.Append(key);
        builder.Append('=');
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append(';');
    }
}
