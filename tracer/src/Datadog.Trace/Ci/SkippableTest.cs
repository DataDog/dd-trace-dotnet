// <copyright file="SkippableTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci;

internal readonly struct SkippableTest
{
    [JsonProperty("name")]
    public readonly string Name;

    [JsonProperty("suite")]
    public readonly string Suite;

    [JsonProperty("parameters")]
    public readonly string? RawParameters;

    [JsonProperty("configurations")]
    public readonly TestsConfigurations? Configurations;

    /// <summary>
    /// Indicates that the backend cannot provide line coverage for this skippable test.
    /// </summary>
    [JsonProperty("_missing_line_code_coverage")]
    public readonly bool MissingLineCodeCoverage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkippableTest"/> struct.
    /// </summary>
    /// <param name="name">Test name returned by the backend.</param>
    /// <param name="suite">Test suite returned by the backend.</param>
    /// <param name="parameters">Serialized test parameters returned by the backend.</param>
    /// <param name="configurations">Backend test configurations used to scope the candidate.</param>
    /// <param name="missingLineCodeCoverage">Whether the backend is missing line coverage for this candidate.</param>
    public SkippableTest(string name, string suite, string? parameters, TestsConfigurations? configurations, bool missingLineCodeCoverage = false)
    {
        Name = name;
        Suite = suite;
        RawParameters = parameters;
        Configurations = configurations;
        MissingLineCodeCoverage = missingLineCodeCoverage;
    }

    /// <summary>
    /// Gets parsed test parameters for matching framework test cases to backend skippable candidates.
    /// </summary>
    /// <returns>Parsed test parameters, or null when the backend candidate has no parameter payload.</returns>
    public TestParameters? GetParameters()
    {
        return string.IsNullOrWhiteSpace(RawParameters) ? null : JsonHelper.DeserializeObject<TestParameters>(RawParameters!);
    }
}
