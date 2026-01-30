// <copyright file="ValueType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.ComponentModel;

namespace Datadog.Trace.FeatureFlags;

/// <summary> Evaluation result type </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Browsable(false)]
#if INTERNAL_FFE
internal enum ValueType
#else
public enum ValueType
#endif
{
    /// <summary> Integer numeric value </summary>
    Integer,

    /// <summary> Float numeric value </summary>
    Numeric,

    /// <summary> Simple string </summary>
    String,

    /// <summary> Bool value </summary>
    Boolean,

    /// <summary> Json value </summary>
    Json
}
