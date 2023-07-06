// <copyright file="CustomSettingsForTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Security.Unit.Tests.Iast
{
    internal class CustomSettingsForTests : IConfigurationSource
    {
        public CustomSettingsForTests(Dictionary<string, object> settings)
        {
            CustomSettings = settings;
        }

        public Dictionary<string, object> CustomSettings { get; }

        public bool? GetBool(string key)
        {
            CustomSettings.TryGetValue(key, out object result);
            return result as bool? ?? null;
        }

        public IDictionary<string, string> GetDictionary(string key)
        {
            return null;
        }

        public IDictionary<string, string> GetDictionary(string key, bool allowOptionalMappings)
        {
            return null;
        }

        public unsafe IDictionary<string, string> GetDictionary(string key, delegate*<ref string, ref string, bool> selector)
        {
            return null;
        }

        public unsafe IDictionary<string, string> GetDictionary(string key, bool allowOptionalMappings, delegate*<ref string, ref string, bool> selector)
        {
            return null;
        }

        public double? GetDouble(string key)
        {
            CustomSettings.TryGetValue(key, out object result);
            return result as double? ?? null;
        }

        public int? GetInt32(string key)
        {
            CustomSettings.TryGetValue(key, out object result);
            return result as int? ?? null;
        }

        public string GetString(string key)
        {
            CustomSettings.TryGetValue(key, out object result);
            return result?.ToString() ?? null;
        }
    }
}
