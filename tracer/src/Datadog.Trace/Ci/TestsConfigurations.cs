// <copyright file="TestsConfigurations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci;

internal readonly struct TestsConfigurations
{
    [JsonProperty(CommonTags.OSPlatform)]
    public readonly string OSPlatform;

    [JsonProperty(CommonTags.OSVersion)]
    public readonly string OSVersion;

    [JsonProperty(CommonTags.OSArchitecture)]
    public readonly string OSArchitecture;

    [JsonProperty(CommonTags.RuntimeName)]
    public readonly string? RuntimeName;

    [JsonProperty(CommonTags.RuntimeVersion)]
    public readonly string? RuntimeVersion;

    [JsonProperty(CommonTags.RuntimeArchitecture)]
    public readonly string? RuntimeArchitecture;

    [JsonProperty("custom")]
    public readonly Dictionary<string, string>? Custom;

    public TestsConfigurations(string osPlatform, string osVersion, string osArchitecture, string?runtimeName, string? runtimeVersion, string? runtimeArchitecture, Dictionary<string, string>? custom)
    {
        OSPlatform = osPlatform;
        OSVersion = osVersion;
        OSArchitecture = osArchitecture;
        RuntimeName = runtimeName;
        RuntimeVersion = runtimeVersion;
        RuntimeArchitecture = runtimeArchitecture;
        Custom = custom;
    }
}
