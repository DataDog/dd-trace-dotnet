// <copyright file="DoubleExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ExtensionMethods;

internal static class DoubleExtensions
{
    /// <summary>
    /// Ensure any double value is a valid percentage number in the range [0,100]
    /// </summary>
    /// <param name="value">Original percentage value</param>
    /// <returns>Sanitized value</returns>
    public static double ToValidPercentage(this double value)
    {
        if (double.IsNaN(value) || value < 0)
        {
            return 0;
        }

        return value > 100 ? 100 : value;
    }
}
