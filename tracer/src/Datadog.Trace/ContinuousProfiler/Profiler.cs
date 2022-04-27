// <copyright file="Profiler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;

namespace Datadog.Trace.ContinuousProfiler
{
    internal class Profiler
    {
        private static Profiler _instance;

        private Profiler(ContextTracker contextTracker, ProfilerStatus status)
        {
            ContextTracker = contextTracker;
            Status = status;
        }

        public static Profiler Instance
        {
            get { return LazyInitializer.EnsureInitialized(ref _instance, () => Create()); }
        }

        public ProfilerStatus Status { get; }

        public ContextTracker ContextTracker { get; }

        private static Profiler Create()
        {
            var status = new ProfilerStatus();
            var contextTracker = new ContextTracker(status);
            return new Profiler(contextTracker, status);
        }
    }
}
