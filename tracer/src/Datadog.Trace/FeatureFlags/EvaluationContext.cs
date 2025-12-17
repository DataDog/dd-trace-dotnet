// <copyright file="EvaluationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary> Standard implementation of a EvaluationContext </summary>
    /// <param name="key"> Targeting Key </param>
    /// <param name="values"> Context optional parameters </param>
    public class EvaluationContext(string key, IDictionary<string, object?>? values = null)
        : IEvaluationContext
    {
        /// <summary> Gets the Context Targeting Key </summary>
        public string TargetingKey { get; } = key;

        /// <summary> Gets the Context optional Values </summary>
        public IDictionary<string, object?> Attributes { get; } = values ?? new Dictionary<string, object?>();

        /// <summary> Get the Context value if existent </summary>
        /// <param name="key"> Value key </param>
        /// <returns> Returns Context Value or null </returns>
        public object? GetAttribute(string key)
        {
            if (Attributes is null || !Attributes.TryGetValue(key, out var res))
            {
                return null;
            }

            return res;
        }
    }
}
