using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Emit
{
    internal static class ModuleLookup
    {
        private static readonly ILog Log = LogProvider.GetLogger(typeof(ModuleLookup));

        private static ManualResetEventSlim _populationResetEvent = new ManualResetEventSlim(initialState: true);
        private static ConcurrentDictionary<Guid, Module> _modules = new ConcurrentDictionary<Guid, Module>();

        public static Module Get(Guid moduleVersionId)
        {
            Module value = null;

            // Block if a population event is happening
            _populationResetEvent.Wait();

            if (_modules.TryGetValue(moduleVersionId, out value))
            {
                return value;
            }

            // Block threads on this event
            _populationResetEvent.Reset();

            try
            {
                PopulateModules();
            }
            catch (Exception ex)
            {
                Log.Error("Error when populating modules.", ex);
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
