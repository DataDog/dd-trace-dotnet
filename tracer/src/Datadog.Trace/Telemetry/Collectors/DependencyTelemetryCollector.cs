// <copyright file="DependencyTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        public void AssemblyLoaded(AssemblyName assembly)
        {
            // TODO: Filter out assemblies we don't care about
            var key = new DependencyTelemetryData { Name = assembly.Name, Version = assembly.Version?.ToString() };
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

        private void SetHasChanges()
        {
            Interlocked.Exchange(ref _hasChangesFlag, 1);
        }
    }
}
