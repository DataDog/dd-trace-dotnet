// <copyright file="ExceptionCaseScheduler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Logging;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionCaseScheduler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ExceptionCaseScheduler>();
        private readonly List<ScheduledException> _scheduledExceptions = new();
        private readonly object _lock = new();
        private readonly Timer _timer;

        public ExceptionCaseScheduler()
        {
            _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Schedule(TrackedExceptionCase doneCase, TimeSpan delay)
        {
            var dueTime = DateTime.UtcNow.Add(delay);
            var scheduledTask = new ScheduledException { Case = doneCase, DueTime = dueTime };

            lock (_lock)
            {
                _scheduledExceptions.Add(scheduledTask);
                _scheduledExceptions.Sort();
                if (_scheduledExceptions[0] == scheduledTask)
                {
                    SetNextTimer(dueTime);
                }
            }
        }

        private void TimerCallback(object? state)
        {
            try
            {
                SafeTimerCallback();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "There was an error while processing the Exception Cases scheduler.");
            }
        }

        private void SafeTimerCallback()
        {
            var casesToInstrument = new List<TrackedExceptionCase>();

            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var dueTasks = _scheduledExceptions.TakeWhile(e => e.DueTime <= now).ToList();
                foreach (var task in dueTasks)
                {
                    if (task.Case != null)
                    {
                        casesToInstrument.Add(task.Case);
                        _scheduledExceptions.Remove(task);
                    }
                }

                if (_scheduledExceptions.Any())
                {
                    SetNextTimer(_scheduledExceptions[0].DueTime);
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
            _timer?.Change((int)delay, Timeout.Infinite);
        }

        private class ScheduledException : IComparable<ScheduledException>
        {
            public TrackedExceptionCase? Case { get; set; }

            public DateTime DueTime { get; set; }

            public int CompareTo(ScheduledException? other)
            {
                if (other is null)
                {
                    return 1;
                }

                return DueTime.CompareTo(other.DueTime);
            }
        }
    }
}
