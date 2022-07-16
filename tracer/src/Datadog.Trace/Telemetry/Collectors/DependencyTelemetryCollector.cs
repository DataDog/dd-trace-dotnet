// <copyright file="DependencyTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.Telemetry
{
    internal class DependencyTelemetryCollector
    {
        private readonly ConcurrentDictionary<DependencyTelemetryData, bool> _assemblies = new();

        private int _hasChangesFlag = 0;

        /// <summary>
        /// Called when an assembly is loaded
        /// </summary>
        public void AssemblyLoaded(Assembly assembly)
        {
            if (!assembly.IsDynamic)
            {
                AssemblyLoaded(assembly.GetName());
            }
        }

        // Internal for testing
        internal void AssemblyLoaded(AssemblyName assembly)
        {
            // exclude dlls we're not interested in which have a "random" component
            // ASP.NET sites generate an App_Web_*.dll with a random string for
            var assemblyName = assembly.Name;
            if (assemblyName is null or ""
             || IsTempPathPattern(assemblyName)
             || (assemblyName[0] == 'A'
              && (assemblyName.StartsWith("App_Web_", StringComparison.Ordinal)
               || assemblyName.StartsWith("App_Theme_", StringComparison.Ordinal)
               || assemblyName.StartsWith("App_GlobalResources.", StringComparison.Ordinal)
               || assemblyName.StartsWith("App_global.asax.", StringComparison.Ordinal)
               || assemblyName.StartsWith("App_Code.", StringComparison.Ordinal)
               || assemblyName.StartsWith("App_WebReferences.", StringComparison.Ordinal)))
             || (assemblyName.Length == 36
              && assemblyName[8] == '-'
              && assemblyName[13] == '-'
              && assemblyName[18] == '-'
              && assemblyName[23] == '-'))
            {
                return;
            }

            var key = new DependencyTelemetryData(name: assemblyName) { Version = assembly.Version?.ToString() };
            if (_assemblies.TryAdd(key, true))
            {
                SetHasChanges();
            }
        }

        public bool HasChanges()
        {
            return _hasChangesFlag == 1;
        }

        /// <summary>
        /// Get the latest data to send to the intake.
        /// </summary>
        /// <returns>Null if there are no changes, or the collector is not yet initialized</returns>
        public ICollection<DependencyTelemetryData> GetData()
        {
            var hasChanges = Interlocked.CompareExchange(ref _hasChangesFlag, 0, 1) == 1;
            if (!hasChanges)
            {
                return null;
            }

            return _assemblies.Keys;
        }

        private static bool IsTempPathPattern(string assemblyName)
        {
            return assemblyName.Length == 12 // (8 + 1 + 3)
                && assemblyName[8] == '.'
                && IsBase32Char(assemblyName[0])
                && IsBase32Char(assemblyName[1])
                && IsBase32Char(assemblyName[2])
                && IsBase32Char(assemblyName[3])
                && IsBase32Char(assemblyName[4])
                && IsBase32Char(assemblyName[5])
                && IsBase32Char(assemblyName[6])
                && IsBase32Char(assemblyName[7])
                && IsBase32Char(assemblyName[9])
                && IsBase32Char(assemblyName[10])
                && IsBase32Char(assemblyName[11]);

            static bool IsBase32Char(char c)
            {
                return c switch
                {
                    >= 'a' and <= 'z' => true,
                    >= '0' and <= '5' => true,
                    _ => false
                };
            }
        }

        private void SetHasChanges()
        {
            Interlocked.Exchange(ref _hasChangesFlag, 1);
        }
    }
}
