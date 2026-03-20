// <copyright file="RuntimeId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util
{
    internal static class RuntimeId
    {
        // Internal propagation env var, not user-configurable — intentionally not in ConfigurationKeys.
        internal const string RootSessionEnvVar = "_DD_ROOT_DOTNET_SESSION_ID";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeId));
        private static string _runtimeId;
        private static string _rootSessionId;

        public static string Get() => LazyInitializer.EnsureInitialized(ref _runtimeId, () => GetRuntimeIdImpl());

        public static string GetRootSessionId() => LazyInitializer.EnsureInitialized(ref _rootSessionId, () => GetRootSessionIdImpl());

        private static string GetRuntimeIdImpl()
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
#pragma warning disable DD0011 // internal propagation env var, not user-configurable
            var inherited = EnvironmentHelpers.GetEnvironmentVariable(RootSessionEnvVar);
#pragma warning restore DD0011
            if (!string.IsNullOrEmpty(inherited))
            {
                Log.Debug("Inherited root session ID from parent: {RootSessionId}", inherited);
                return inherited;
            }

            var rootId = Get();
            EnvironmentHelpers.SetEnvironmentVariable(RootSessionEnvVar, rootId);
            return rootId;
        }
    }
}
