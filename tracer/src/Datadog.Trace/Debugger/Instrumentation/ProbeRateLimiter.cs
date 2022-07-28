// <copyright file="ProbeRateLimiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    internal class ProbeRateLimiter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeRateLimiter));
        private static object _globalInstanceLock = new();
        private static bool _globalInstanceInitialized;
        private static ProbeRateLimiter _instance;

        public ProbeRateLimiter()
        {
        }

        internal bool IsLimitReached { get; private set; }

        public static ProbeRateLimiter Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock);
            }
        }

        internal void UpdateLimitReached(bool value)
        {
            IsLimitReached = value;
        }
    }
}
