// <copyright file="LiveDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger
{
    internal class LiveDebugger
    {
        private static readonly DebuggerSettings _settings = DebuggerSettings.FromDefaultSources();
        private static int _firstInitialization = 1;
        private static Lazy<bool> _enabledLazy = new Lazy<bool>(() => InternalEnabled(), true);
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LiveDebugger));

        public static bool Enabled => _enabledLazy.Value;

        public static bool IsRunning => Interlocked.CompareExchange(ref _firstInitialization, 0, 0) == 0;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // Initialize() was already called before
                return;
            }

            Log.Information("Initializing Live Debugger");
        }

        private static bool InternalEnabled()
        {
            if (_settings.Enabled)
            {
                Log.Information("Live Debugger Enabled by Configuration");
                return true;
            }

            return false;
        }
    }
}
