// <copyright file="ValueUsageInfoExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Microsoft.CodeAnalysis;

internal static class ValueUsageInfoExtensions
{
    public static bool IsReadFrom(this ValueUsageInfo valueUsageInfo)
        => (valueUsageInfo & ValueUsageInfo.Read) != 0;

    public static bool IsWrittenTo(this ValueUsageInfo valueUsageInfo)
        => (valueUsageInfo & ValueUsageInfo.Write) != 0;

    public static bool IsNameOnly(this ValueUsageInfo valueUsageInfo)
        => (valueUsageInfo & ValueUsageInfo.Name) != 0;

    public static bool IsReference(this ValueUsageInfo valueUsageInfo)
        => (valueUsageInfo & ValueUsageInfo.Reference) != 0;
}
