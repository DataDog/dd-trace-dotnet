// <copyright file="MemoryPressureConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.RateLimiting
{
    internal readonly struct MemoryPressureConfig
    {
        public static MemoryPressureConfig Default { get; } = new()
        {
            HighPressureThresholdRatio = 0.85,
            MaxGen2PerSecond = 2,
            MemoryExitMargin = 0.05,
            Gen2ExitMargin = 1,
            ConsecutiveHighToEnter = 1,
            ConsecutiveLowToExit = 1,
            RefreshInterval = TimeSpan.FromSeconds(1)
        };

        // Enter high pressure at this fraction (0.0–1.0) of available memory in use. Machine-wide on .NET Framework.
        public double HighPressureThresholdRatio { get; init; }

        public int MaxGen2PerSecond { get; init; }

        public double MemoryExitMargin { get; init; }

        public int Gen2ExitMargin { get; init; }

        public int ConsecutiveHighToEnter { get; init; }

        public int ConsecutiveLowToExit { get; init; }

        public TimeSpan RefreshInterval { get; init; }

        public override string ToString()
        {
            var culture = CultureInfo.InvariantCulture;
            var sb = StringBuilderCache.Acquire();

            sb.Append("Threshold=");
            sb.Append(HighPressureThresholdRatio.ToString("F2", culture));
            sb.Append(" (");
            sb.Append((HighPressureThresholdRatio * 100).ToString("F1", culture));
            sb.Append("%), MaxGen2=");
            sb.Append(MaxGen2PerSecond.ToString(culture));
            sb.Append("/s, ExitMargin=");
            sb.Append(MemoryExitMargin.ToString("F2", culture));
            sb.Append(", Gen2ExitMargin=");
            sb.Append(Gen2ExitMargin.ToString(culture));
            sb.Append(", HighToEnter=");
            sb.Append(ConsecutiveHighToEnter.ToString(culture));
            sb.Append(", LowToExit=");
            sb.Append(ConsecutiveLowToExit.ToString(culture));

            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
