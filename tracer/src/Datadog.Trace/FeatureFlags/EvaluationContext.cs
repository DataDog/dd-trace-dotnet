// <copyright file="EvaluationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags
{
    internal class EvaluationContext(string key, Dictionary<string, object>? values = null, Func<object?, object?>? convertValue = null)
    {
        private readonly Func<object?, object?>? _convertValue = convertValue;

        public string TargetingKey { get; } = key;

        public Dictionary<string, object> Values { get; } = values ?? new Dictionary<string, object>();

        public object? GetValue(string key)
        {
            if (Values is null || !Values.TryGetValue(key, out var res))
            {
                return null;
            }

            return res;
        }

        public object? ConvertValue(object? value)
        {
            return _convertValue?.Invoke(value);
        }
    }
}
