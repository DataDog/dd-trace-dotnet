// <copyright file="SkippeableTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci;

internal readonly struct SkippeableTest
{
    [JsonProperty("name")]
    public readonly string Name;

    [JsonProperty("suite")]
    public readonly string Suite;

    [JsonProperty("parameters")]
    public readonly string RawParameters;

    [JsonProperty("configuration")]
    public readonly Dictionary<string, object> Configuration;

    public SkippeableTest(string name, string suite, string parameters, Dictionary<string, object> configuration)
    {
        Name = name;
        Suite = suite;
        RawParameters = parameters;
        Configuration = configuration;
    }

    public TestParameters GetParameters()
    {
        return JsonConvert.DeserializeObject<TestParameters>(RawParameters);
    }
}
