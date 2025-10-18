// <copyright file="TestConfigKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration.ConfigurationSources.Registry;

namespace Datadog.Trace.TestHelpers;

internal readonly struct TestConfigKey(string testKey) : IConfigKey
{
    private readonly string _testKey = testKey;

    public string GetKey() => _testKey;
}
