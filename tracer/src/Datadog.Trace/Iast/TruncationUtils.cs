// <copyright file="TruncationUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Iast;

internal static class TruncationUtils
{
    private const string TRUNCATED = "truncated";
    private const string RIGHT = "right";
    private static int maxValueLength = 250;

    public static void Init(int truncationMaxValueLength)
    {
        if (truncationMaxValueLength > 0)
        {
            maxValueLength = truncationMaxValueLength;
        }
    }

    public static void WriteTruncatableValue(this JsonWriter writer, string? value)
    {
        if (value != null && value.Length > maxValueLength)
        {
            writer.WriteValue(value.Substring(0, maxValueLength));
            writer.WritePropertyName(TRUNCATED);
            writer.WriteValue(RIGHT);
        }
        else
        {
            writer.WriteValue(value);
        }
    }
}
