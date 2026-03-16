// <copyright file="TestScheduler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Debugger.RateLimiting;

namespace Datadog.Trace.Tests.Debugger.RateLimiting
{
    /// <summary>
    /// Test scheduler that allows manual control over when callbacks are invoked.
    /// </summary>
    internal class TestScheduler : ISamplerScheduler, IDisposable
    {
        private readonly System.Collections.Generic.List<(Action Callback, IDisposable Token)> _scheduled = [];
        private bool _disposed;

        public int SubscriptionCount => _scheduled.Count;

        public IDisposable Schedule(Action callback, TimeSpan interval)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TestScheduler));
            }

            var token = new TestScheduleToken(this);
            _scheduled.Add((callback, token));
            return token;
        }

        public void TriggerRefresh()
        {
            if (_disposed)
            {
                return;
            }

            // Create a copy to avoid issues if callbacks modify the list
            var callbacks = _scheduled.Select(x => x.Callback).ToArray();
            foreach (var callback in callbacks)
            {
                callback();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _scheduled.Clear();
        }

        private class TestScheduleToken : IDisposable
        {
            private readonly TestScheduler _scheduler;

            public TestScheduleToken(TestScheduler scheduler)
            {
                _scheduler = scheduler;
            }

            public void Dispose()
            {
                // Remove from scheduler's list
                _scheduler._scheduled.RemoveAll(x => Equals(x.Token, this));
            }
        }
    }
}
