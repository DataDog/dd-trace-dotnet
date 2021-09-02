// <copyright file="StopwatchHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Util
{
    internal class StopwatchHelpers
    {
        private static readonly double DateTimeTickFrequency = 10000000.0 / Stopwatch.Frequency;

        public static TimeSpan GetElapsed(long stopwatchTicks)
        {
            var ticks = (long)(stopwatchTicks * DateTimeTickFrequency);

            return new TimeSpan(ticks);
        }
    }
}
