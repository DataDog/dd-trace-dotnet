// <copyright file="RuntimeId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Util
{
    internal static class RuntimeId
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeId));
        private static string _runtimeId;
        private static string _rootSessionId;

        public static string Get() => LazyInitializer.EnsureInitialized(ref _runtimeId, () => GetImpl());

        public static string GetRootSessionId() => LazyInitializer.EnsureInitialized(ref _rootSessionId, () => GetRootSessionIdImpl());

        [TestingAndPrivateOnly]
        internal static void ResetForTests() => _rootSessionId = null;

        private static string GetImpl()
        {
            if (NativeLoader.TryGetRuntimeIdFromNative(out var runtimeId))
            {
                Log.Information("Runtime id retrieved from native loader: {RuntimeId}", runtimeId);
                return runtimeId;
            }

            var guid = Guid.NewGuid().ToString();
            Log.Debug("Unable to get the runtime id from native. Fallback to Guid.NewGuid() : {NewGuid}", guid);

            return guid;
        }

        private static string GetRootSessionIdImpl()
        {
            var config = new ConfigurationBuilder(new EnvironmentConfigurationSource(), TelemetryFactory.Config);
            var inherited = config.WithKeys(ConfigurationKeys.Telemetry.RootSessionId).AsString();
            if (!string.IsNullOrEmpty(inherited))
            {
                Log.Debug("Inherited root session ID from parent: {RootSessionId}", inherited);
                return inherited;
            }

            var rootId = Get();
            EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.Telemetry.RootSessionId, rootId);
            return rootId;
        }
    }
}
