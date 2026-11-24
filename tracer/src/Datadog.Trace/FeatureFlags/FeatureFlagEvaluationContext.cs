// <copyright file="FeatureFlagEvaluationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags;

internal static class FeatureFlagEvaluationContext
{
    internal static Dictionary<string, string> Create(MutableSettings settings)
        => new()
        {
            ["service"] = settings.DefaultServiceName,
            ["env"] = StringUtil.IsNullOrEmpty(settings.Environment) ? "unknown" : settings.Environment,
            ["version"] = StringUtil.IsNullOrEmpty(settings.ServiceVersion) ? "unknown" : settings.ServiceVersion,
        };
}
