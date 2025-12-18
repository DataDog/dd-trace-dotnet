// <copyright file="ValueType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;

namespace Datadog.Trace.FeatureFlags;

/// <summary> Evaluation result reason </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
public enum ValueType
{
    /// <summary> Integer numeric value </summary>
    INTEGER,

    /// <summary> Float numeric value </summary>
    NUMERIC,

    /// <summary> Simple string </summary>
    STRING,

    /// <summary> Bool value </summary>
    BOOLEAN,

    /// <summary> Json string value </summary>
    JSON
}
