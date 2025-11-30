// <copyright file="ShardRange.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal class ShardRange
{
    public int Start { get; set; }

    public int End { get; set; }
}
