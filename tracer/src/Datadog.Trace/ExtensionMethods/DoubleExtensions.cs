// <copyright file="DoubleExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.ExtensionMethods;

internal static class DoubleExtensions
{
    [return: NotNullIfNotNull("value")]
    internal static double? RoundUp(this double? value, int digits)
    {
        if (value == null)
        {
            return null;
        }

        var pow = Math.Pow(10, digits);
        return Math.Ceiling(value.Value * pow) / pow;
    }
}
