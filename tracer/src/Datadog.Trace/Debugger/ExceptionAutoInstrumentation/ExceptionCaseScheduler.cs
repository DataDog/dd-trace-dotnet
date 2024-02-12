// <copyright file="ExceptionCaseScheduler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionCaseScheduler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ExceptionCaseScheduler>();
        private static readonly List<ScheduledException> ScheduledExceptions = new();
        private static readonly object Lock = new();
        private static Timer _timer;

        public ExceptionCaseScheduler()
        {
            _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Schedule(TrackedExceptionCase doneCase, TimeSpan delay)
        {
            var dueTime = DateTime.UtcNow.Add(delay);
            var scheduledTask = new ScheduledException { Case = doneCase, DueTime = dueTime };

            lock (Lock)
            {
                ScheduledExceptions.Add(scheduledTask);
                ScheduledExceptions.Sort();
                if (ScheduledExceptions[0] == scheduledTask)
                {
                    SetNextTimer(dueTime);
                }
            }
        }

        private void TimerCallback(object state)
        {
            try
            {
                SafeTimerCallback(state);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "There was an error while processing the Exception Cases scheduler.");
            }
        }

        private void SafeTimerCallback(object state)
        {
            var casesToInstrument = new List<TrackedExceptionCase>();

            lock (Lock)
            {
                var now = DateTime.UtcNow;
                var dueTasks = ScheduledExceptions.TakeWhile(e => e.DueTime <= now).ToList();
                foreach (var task in dueTasks)
                {
                    casesToInstrument.Add(task.Case);
                    ScheduledExceptions.Remove(task);
                }

                if (ScheduledExceptions.Any())
                {
                    SetNextTimer(ScheduledExceptions[0].DueTime);
                }
            }

            foreach (var @case in casesToInstrument)
            {
                @case.Instrument();
            }
        }

        private void SetNextTimer(DateTime dueTime)
        {
            var delay = Math.Max((dueTime - DateTime.UtcNow).TotalMilliseconds, 0);
            _timer.Change((int)delay, Timeout.Infinite);
        }

        private class ScheduledException : IComparable<ScheduledException>
        {
            public TrackedExceptionCase Case { get; set; }

            public DateTime DueTime { get; set; }

            public int CompareTo(ScheduledException other)
            {
                return DueTime.CompareTo(other?.DueTime);
            }
        }
    }
}
