using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Facility to share a scope across multiple integrations in the same call context
    /// </summary>
    internal class CrossIntegrationScope
    {
        private readonly AsyncLocalCompat<StrongBox<Scope>> _scope = new AsyncLocalCompat<StrongBox<Scope>>();

        public StrongBox<Scope> Init()
        {
            // Prepare a container that can be filled by integrations further down
            var box = new StrongBox<Scope>();
            _scope.Set(box);
            return box;
        }

        public bool SetScope(Scope scope)
        {
            var container = _scope.Get();

            if (container == null)
            {
                return false;
            }

            container.Value = scope;
            return true;
        }
    }
}
