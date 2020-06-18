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
