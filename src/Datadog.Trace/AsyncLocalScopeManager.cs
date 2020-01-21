using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager : ScopeManagerBase
    {
        private readonly AsyncLocalCompat<Scope> _activeScope = new AsyncLocalCompat<Scope>();

        public override Scope Active
        {
            get
            {
                return _activeScope.Get();
            }

            protected set
            {
                _activeScope.Set(value);
            }
        }
    }
}
