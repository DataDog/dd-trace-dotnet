// <copyright file="ExceptionDebugging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation.ThirdParty;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.dnlib;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionDebugging
    {
        private static ExceptionDebuggingSettings? _settings;
        private static int _firstInitialization = 1;
        private static bool _isDisabled;

        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExceptionDebugging));

        public static ExceptionDebuggingSettings Settings
        {
            get => LazyInitializer.EnsureInitialized(ref _settings, ExceptionDebuggingSettings.FromDefaultSource)!;
            private set => _settings = value;
        }

        public static bool Enabled => Settings.Enabled && !_isDisabled;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                return;
            }

            Log.Information("Initializing Exception Debugging");

            if (!ThirdPartyModules.PopulateFromConfig())
            {
                Log.Warning("Third party modules load has failed. Disabling Exception Debugging.");
                _isDisabled = true;
            }
            else
            {
                ExceptionTrackManager.Initialize();
            }
        }

        public static void Report(Span span, Exception exception)
        {
            if (!Enabled)
            {
                return;
            }

            ExceptionTrackManager.Report(span, exception);
        }

        public static bool TryBeginRequest(out ShadowStackTree? tree)
        {
            if (!Enabled)
            {
                tree = null;
                return false;
            }

            tree = ShadowStackHolder.EnsureShadowStackEnabled();
            tree.Clear();
            tree.Init();
            tree.IsInRequestContext = true;
            return true;
        }

        public static void EndRequest(ShadowStackTree? tree)
        {
            if (!Enabled)
            {
                return;
            }

            tree?.Clear();
        }
    }
}
