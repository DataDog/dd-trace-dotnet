// <copyright file="ManualInstrumentationConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

namespace Datadog.Trace.Configuration.ConfigurationSources;

/// <summary>
/// Wraps the settings passed in from the manual instrumentation API in a configuration source, to make it easier to integrate
/// </summary>
internal sealed class ManualInstrumentationConfigurationSource : ManualInstrumentationConfigurationSourceBase
{
    public ManualInstrumentationConfigurationSource(IReadOnlyDictionary<string, object?> dictionary, bool useDefaultSources)
        : base(dictionary, useDefaultSources)
    {
    }

    protected override bool TryGetValue(string key, out object? value)
    {
        // This is where any "remapping" should be done, in cases
        // where either the manual-instrumentation key differs or the
        // type stored in the dictionary differs

        var result = base.TryGetValue(key, out value);
        if (result)
        {
            if (value is not null)
            {
                value = RemapResult(key, value);
            }
        }

        return result;
    }

    private static object RemapResult(string key, object value) => key switch
    {
        TracerSettingKeyConstants.AgentUriKey => value is Uri uri ? uri.ToString() : value,
        TracerSettingKeyConstants.DisabledIntegrationNamesKey => value is HashSet<string> set ? string.Join(";", set) : value,
        TracerSettingKeyConstants.HttpServerErrorCodesKey => value is List<int> list
                                                                 ? string.Join(",", list.Select(i => i.ToString(CultureInfo.InvariantCulture)))
                                                                 : value,
        TracerSettingKeyConstants.HttpClientErrorCodesKey => value is List<int> list
                                                                 ? string.Join(",", list.Select(i => i.ToString(CultureInfo.InvariantCulture)))
                                                                 : value,
        _ => value,
    };
}
