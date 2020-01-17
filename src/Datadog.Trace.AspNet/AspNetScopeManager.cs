using System;
using System.Web;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AspNet
{
    internal class AspNetScopeManager : ScopeManagerBase
    {
        private readonly string _name = "__Datadog_Scope_Current__" + Guid.NewGuid();
        private readonly AsyncLocalCompat<Scope> _activeScopeFallback = new AsyncLocalCompat<Scope>();

        public override Scope Active
        {
            get
            {
                var activeScope = HttpContext.Current?.Items[_name] as Scope;
                if (activeScope != null)
                {
                    return activeScope;
                }

                return _activeScopeFallback.Get();
            }

            set
            {
                var httpContext = HttpContext.Current;
                if (httpContext != null)
                {
                    httpContext.Items[_name] = value;
                }

                _activeScopeFallback.Set(value);
            }
        }
    }
}
