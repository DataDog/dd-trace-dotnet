// <copyright file="ExperimentationOptOut.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags;

internal static class ExperimentationOptOut
{
    internal const string Attribute = "dd_experimentation_opt_out_at";

    internal static bool TryExclude(IDictionary<string, object?>? attributes, out bool exclude)
    {
        exclude = false;
        if (attributes is null || !attributes.TryGetValue(Attribute, out var value))
        {
            return true;
        }

        if (value is not string timestamp)
        {
            return false;
        }

        try
        {
            // Use the allocation startAt/endAt parser's timestamp contract.
            FeatureFlagsEvaluator.ParseDate(timestamp);
            exclude = true;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
