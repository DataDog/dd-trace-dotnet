// <copyright file="EvaluationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.FeatureFlags.FlagEvaluation;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary> Standard implementation of a EvaluationContext </summary>
    /// <param name="key"> Targeting Key </param>
    /// <param name="attributes"> Context optional attributes </param>
    /// <param name="contextOmissions"> Omissions recorded while adapting the caller's context </param>
    internal sealed class EvaluationContext(string? key, IDictionary<string, object?>? attributes = null, ContextOmissionReason contextOmissions = ContextOmissionReason.None)
    {
        /// <summary> Gets the Context Targeting Key </summary>
        public string? TargetingKey { get; } = key;

        /// <summary> Gets the Context optional Values </summary>
        public IDictionary<string, object?> Attributes { get; } = attributes ?? new Dictionary<string, object?>();

        /// <summary> Gets omissions already recorded while adapting the caller's context. </summary>
        public ContextOmissionReason ContextOmissions { get; } = contextOmissions;

        /// <summary> Get the Context value if existent </summary>
        /// <param name="key"> Value key </param>
        /// <returns> Returns Context Value or null </returns>
        public object? GetAttribute(string key)
            => Attributes.TryGetValue(key, out var res) ? res : null;
    }
}
