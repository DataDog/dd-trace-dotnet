using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Emit
{
    internal static class ModuleLookup
    {
        /// <summary>
        /// Some naive upper limit to resolving assemblies that we can use to stop making expensive calls.
        /// </summary>
        private const int MaxFailures = 50;

        private static readonly Vendoring.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(ModuleLookup));

        private static ManualResetEventSlim _populationResetEvent = new ManualResetEventSlim(initialState: true);
        private static ConcurrentDictionary<Guid, Module> _modules = new ConcurrentDictionary<Guid, Module>();

        private static int _failures = 0;
        private static bool _shortCircuitLogicHasLogged = false;

        public static Module Get(Guid moduleVersionId)
        {
            // First attempt at cached values with no blocking
            if (_modules.TryGetValue(moduleVersionId, out Module value))
            {
                return value;
            }

            // Block if a population event is happening
            _populationResetEvent.Wait();

            // See if the previous population event populated what we need
            if (_modules.TryGetValue(moduleVersionId, out value))
            {
                return value;
            }

            if (_failures >= MaxFailures)
            {
                // For some unforeseeable reason we have failed on a lot of AppDomain lookups
                if (!_shortCircuitLogicHasLogged)
                {
                    Log.Warning("Datadog is unable to continue attempting module lookups for this AppDomain. Falling back to legacy method lookups.");
                }

                return null;
            }

            // Block threads on this event
            _populationResetEvent.Reset();

            try
            {
                PopulateModules();
            }
            catch (Exception ex)
            {
                _failures++;
                Log.Error(ex, "Error when populating modules.");
            }
            finally
            {
                // Continue threads blocked on this event
                _populationResetEvent.Set();
            }

            _modules.TryGetValue(moduleVersionId, out value);

            return value;
        }

        private static void PopulateModules()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                foreach (var module in assembly.Modules)
                {
                    _modules.TryAdd(module.ModuleVersionId, module);
                }
            }
        }
    }
}
