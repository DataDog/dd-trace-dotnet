// <copyright file="SkippableTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

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

    public SkippableTest(string name, string suite, string? parameters, TestsConfigurations? configurations)
    {
        Name = name;
        Suite = suite;
        RawParameters = parameters;
        Configurations = configurations;
    }

    public TestParameters? GetParameters()
    {
        return string.IsNullOrWhiteSpace(RawParameters) ? null : JsonConvert.DeserializeObject<TestParameters>(RawParameters!);
    }
}
