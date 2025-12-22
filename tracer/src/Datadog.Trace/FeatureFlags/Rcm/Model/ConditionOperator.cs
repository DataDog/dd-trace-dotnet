// <copyright file="ConditionOperator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

/// <summary> Rule operator. Capitalized for json deserialization support </summary>
internal enum ConditionOperator
{
    LT,
    LTE,
    GT,
    GTE,
    MATCHES,
    NOT_MATCHES,
    ONE_OF,
    NOT_ONE_OF,
    IS_NULL
}
