// <copyright file="Profiler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.ContinuousProfiler
{
    internal sealed class Profiler
    {
        private static Profiler _instance;

        internal Profiler(IContextTracker contextTracker, IProfilerStatus status, ProfilerSettings settings)
        {
            ContextTracker = contextTracker;
            Status = status;
            Settings = settings;
        }

        public static Profiler Instance
        {
            get { return LazyInitializer.EnsureInitialized(ref _instance, () => Create()); }
        }

        public ProfilerSettings Settings { get; }

        public IProfilerStatus Status { get; }

        public IContextTracker ContextTracker { get; }

        internal static void SetInstanceForTests(Profiler value)
        {
            _instance = value;
        }

        private static Profiler Create()
        {
            var settings = new ProfilerSettings(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
            var status = new ProfilerStatus(settings);
            var contextTracker = new ContextTracker(status);
            return new Profiler(contextTracker, status, settings);
        }
    }
}
