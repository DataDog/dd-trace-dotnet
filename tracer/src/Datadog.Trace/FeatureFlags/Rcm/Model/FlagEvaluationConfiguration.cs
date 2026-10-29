// <copyright file="FlagEvaluationConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class FlagEvaluationConfiguration
{
    private object? _key;

    public object? Key
    {
        get => _key;
        set
        {
            KeyPresent = true;
            _key = value;
        }
    }

    internal bool KeyPresent { get; private set; }

    [JsonExtensionData]
    internal IDictionary<string, JToken>? AdditionalProperties { get; set; }
}
