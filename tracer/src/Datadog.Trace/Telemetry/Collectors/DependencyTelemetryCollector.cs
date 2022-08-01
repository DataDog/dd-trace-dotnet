// <copyright file="DependencyTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.Telemetry
{
    internal class DependencyTelemetryCollector
    {
        // value is true when sent to the backend
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
             || IsGuid(assemblyName))
            {
                return;
            }

            var key = new DependencyTelemetryData(name: assemblyName) { Version = assembly.Version?.ToString() };

            if (_assemblies.TryAdd(key, false))
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

            var assembliesToRecord = new List<DependencyTelemetryData>();
            foreach (var assembly in _assemblies)
            {
                if (assembly.Value == false)
                {
                    _assemblies[assembly.Key] = true;
                    assembliesToRecord.Add(assembly.Key);
                }
            }

            return assembliesToRecord;
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

        private static bool IsGuid(string assemblyName)
        {
            switch (assemblyName.Length)
            {
                // Simple implementation to remove guids
                case 36 when assemblyName[8] == '-' && assemblyName[13] == '-' && assemblyName[18] == '-' && assemblyName[23] == '-':
                // and other weird use cases like ℛ*710fa04a-6428-4dd1-85a0-0419c142709b#5-0 where the suffix can be up to 7 chars long
                case >= 42 when assemblyName[10] == '-' && assemblyName[15] == '-' && assemblyName[20] == '-' && assemblyName[25] == '-':
                    return true;
                default:
                    return false;
            }
        }

        private void SetHasChanges()
        {
            Interlocked.Exchange(ref _hasChangesFlag, 1);
        }
    }
}
