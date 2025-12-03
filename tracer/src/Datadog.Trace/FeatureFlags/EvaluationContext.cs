// <copyright file="EvaluationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary> Estandard implementation of a EvaluationContext </summary>
    /// <param name="key"> Targeting Key </param>
    /// <param name="values"> Context optional parameters </param>
    /// <param name="convertValue"> Context optional convert value function </param>
    public class EvaluationContext(string key, IDictionary<string, object?>? values = null, Func<object?, object?>? convertValue = null)
        : IEvaluationContext
    {
        private readonly Func<object?, object?>? _convertValue = convertValue;

        /// <summary> Gets the Context Targeting Key </summary>
        public string TargetingKey { get; } = key;

        /// <summary> Gets the Context optional Values </summary>
        public IDictionary<string, object?> Values { get; } = values ?? new Dictionary<string, object?>();

        /// <summary> Get the Context value if existent </summary>
        /// <param name="key"> Value key </param>
        /// <returns> Returns Context Value or null </returns>
        public object? GetValue(string key)
        {
            if (Values is null || !Values.TryGetValue(key, out var res))
            {
                return null;
            }

            return res;
        }

        /// <summary> Optional value conversion function </summary>
        /// <param name="value"> Value to convert </param>
        /// <returns> Converted value </returns>
        public object? ConvertValue(object? value)
        {
            return _convertValue?.Invoke(value);
        }
    }
}
