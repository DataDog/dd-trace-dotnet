// <copyright file="NullConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Configuration;

internal class NullConfigurationSource : IConfigurationSource
{
    public static readonly NullConfigurationSource Instance = new();

    public string? GetString(string key) => null;

    public int? GetInt32(string key) => null;

    public double? GetDouble(string key) => null;

    public bool? GetBool(string key) => null;

    public IDictionary<string, string>? GetDictionary(string key) => null;

    public IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings) => null;

    public unsafe IDictionary<string, string>? GetDictionary(string key, delegate*<ref string, ref string, bool> selector) => null;

    public unsafe IDictionary<string, string>? GetDictionary(string key, bool allowOptionalMappings, delegate*<ref string, ref string, bool> selector) => null;
}
