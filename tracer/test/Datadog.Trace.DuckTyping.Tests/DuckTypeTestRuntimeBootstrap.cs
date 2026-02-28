// <copyright file="DuckTypeTestRuntimeBootstrap.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DuckTyping.Tests
{
    internal static class DuckTypeTestRuntimeBootstrap
    {
        private const string TestModeEnvironmentVariable = "DD_DUCKTYPE_TEST_MODE";
        private const string AotModeValue = "aot";
        private const string DynamicModeValue = "dynamic";
        private const string AotRegistryPathEnvironmentVariable = "DD_DUCKTYPE_AOT_REGISTRY_PATH";
        private const string AotBootstrapTypeFullName = "Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap";
        private const string AotBootstrapInitializeMethod = "Initialize";

        private static int _initialized;

        internal static void Initialize()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            {
                return;
            }

            var mode = Environment.GetEnvironmentVariable(TestModeEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(mode))
            {
                return;
            }

            if (string.Equals(mode, DynamicModeValue, StringComparison.OrdinalIgnoreCase))
            {
                DuckType.ResetRuntimeModeForTests();
                return;
            }

            if (!string.Equals(mode, AotModeValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DuckType.ResetRuntimeModeForTests();

            var registryPath = Environment.GetEnvironmentVariable(AotRegistryPathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(registryPath))
            {
                DuckType.EnableAotMode();
                return;
            }

            var fullRegistryPath = Path.GetFullPath(registryPath);
            if (!File.Exists(fullRegistryPath))
            {
                throw new FileNotFoundException(
                    $"AOT registry assembly was not found at '{fullRegistryPath}'.",
                    fullRegistryPath);
            }

            var registryAssembly = Assembly.LoadFrom(fullRegistryPath);
            var bootstrapType = registryAssembly.GetType(AotBootstrapTypeFullName, throwOnError: false);
            if (bootstrapType is null)
            {
                bootstrapType = registryAssembly
                               .GetTypes()
                               .FirstOrDefault(type => string.Equals(type.Name, "DuckTypeAotRegistryBootstrap", StringComparison.Ordinal));
            }

            var initializeMethod = bootstrapType?.GetMethod(AotBootstrapInitializeMethod, BindingFlags.Public | BindingFlags.Static);
            initializeMethod?.Invoke(obj: null, parameters: null);
        }
    }
}
