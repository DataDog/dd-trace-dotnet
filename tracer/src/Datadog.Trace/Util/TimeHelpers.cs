// <copyright file="TimeHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Util;

internal static class TimeHelpers
{
    public static long MillisecondsToNanoseconds(long milliseconds)
        => checked(milliseconds * 1_000_000);

    public static long NanosecondsToMilliseconds(long nanoseconds)
        => nanoseconds / 1_000_000;
}
