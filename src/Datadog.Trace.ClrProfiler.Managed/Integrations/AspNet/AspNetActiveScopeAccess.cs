#if !NETSTANDARD2_0

using System;
using System.Web;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal class AspNetActiveScopeAccess : IActiveScopeAccess
    {
        private readonly string _reservedHttpContextKey = $"__Data_dog_scope__{Guid.NewGuid()}";

        public long CreatedAtTicks { get; } = DateTime.Now.Ticks;

        public Guid? ContextGuid => null;

        public int Priority => 10;

        public Scope GetActiveScope(params object[] parameters)
        {
            var activeScope = HttpContext.Current?.Items[_reservedHttpContextKey] as Scope;
            return activeScope;
        }

        public bool TrySetActiveScope(Scope scope, params object[] parameters)
        {
            try
            {
                var httpContext = HttpContext.Current;
                if (httpContext != null)
                {
                    httpContext.Items[_reservedHttpContextKey] = scope;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
