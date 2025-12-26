// <copyright file="ConditionConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class ConditionConfiguration
{
    public ConditionOperator? Operator { get; set; }

    public string? Attribute { get; set; }

    public object? Value { get; set; }
}
