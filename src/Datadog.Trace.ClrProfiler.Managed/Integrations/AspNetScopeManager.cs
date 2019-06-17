#if !NETSTANDARD2_0

using System;
using System.Collections.Generic;
using System.Web;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal class AspNetScopeManager : IScopeManager
    {
        private static readonly ILog Log = LogProvider.For<AspNetScopeManager>();

        private readonly string _name = "__Datadog_Scope_Current__" + Guid.NewGuid();
        private readonly AsyncLocalCompat<Scope> _activeScopeFallback = new AsyncLocalCompat<Scope>();

        public event EventHandler<ScopeEventArgs> ScopeOpened;

        public event EventHandler<ScopeEventArgs> ScopeActivated;

        public event EventHandler<ScopeEventArgs> ScopeDeactivated;

        public event EventHandler<ScopeEventArgs> ScopeClosed;

        public Scope Active
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
        }

        public Scope Activate(Span span, bool finishOnClose)
        {
            var activeScope = Active;
            var scope = new Scope(activeScope, span, this, finishOnClose);

            var scopeDeactivatedArgs = new ScopeEventArgs(activeScope);
            var scopeOpenedArgs = new ScopeEventArgs(scope);

            ScopeOpened?.Invoke(this, scopeOpenedArgs);

            SetScope(scope);
            ScopeDeactivated?.Invoke(this, scopeDeactivatedArgs);
            ScopeActivated?.Invoke(this, scopeOpenedArgs);

            return scope;
        }

        public void Close(Scope scope)
        {
            var current = Active;
            if (current != null && current == scope)
            {
                // if the scope that was just closed was the active scope,
                // set its parent as the new active scope
                SetScope(current.Parent);
                ScopeDeactivated?.Invoke(this, new ScopeEventArgs(current));

                if (current.Parent != null)
                {
                    ScopeActivated?.Invoke(this, new ScopeEventArgs(current.Parent));
                }
            }

            ScopeClosed?.Invoke(this, new ScopeEventArgs(scope));
        }

        private void SetScope(Scope scope)
        {
            var httpContext = HttpContext.Current;
            if (httpContext != null)
            {
                httpContext.Items[_name] = scope;
            }

            _activeScopeFallback.Set(scope);
        }
    }
}
#endif
