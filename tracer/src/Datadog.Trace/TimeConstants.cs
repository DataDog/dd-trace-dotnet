// <copyright file="TimeConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    internal static class TimeConstants
    {
        public const long NanoSecondsPerTick = 1000000 / TimeSpan.TicksPerMillisecond;

        public const long UnixEpochInTicks = 621355968000000000; // = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks
    }
}
