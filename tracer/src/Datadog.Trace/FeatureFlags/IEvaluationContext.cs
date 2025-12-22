// <copyright file="IEvaluationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.ComponentModel;

namespace Datadog.Trace.FeatureFlags;

/// <summary>FeatureFlag Evaluation result.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
public partial interface IEvaluationContext
{
    /// <summary> Gets the targeting key.</summary>
    string TargetingKey { get; }

    /// <summary> Gets context attributes.</summary>
    public IDictionary<string, object?> Attributes { get; }

    /// <summary> Get the Context attribute if existent </summary>
    /// <param name="key"> Value key </param>
    /// <returns> Returns Context Value or null </returns>
    object? GetAttribute(string key);
}
