using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager : IScopeManager
    {
        private static readonly ILog Log = LogProvider.For<AsyncLocalScopeManager>();

        private readonly AsyncLocalCompat<Scope> _activeScope = new AsyncLocalCompat<Scope>();

        public event EventHandler<ScopeEventArgs> ScopeOpened;

        public event EventHandler<ScopeEventArgs> ScopeActivated;

        public event EventHandler<ScopeEventArgs> ScopeDeactivated;

        public event EventHandler<ScopeEventArgs> ScopeClosed;

        public Scope Active => _activeScope.Get();

        public Scope Activate(Span span, bool finishOnClose)
        {
            var activeScope = _activeScope.Get();
            var scope = new Scope(activeScope, span, this, finishOnClose);
            var scopeOpenedArgs = new ScopeEventArgs(scope);

            ScopeOpened?.Invoke(this, scopeOpenedArgs);
            _activeScope.Set(scope);

            if (activeScope != null)
            {
                ScopeDeactivated?.Invoke(this, new ScopeEventArgs(activeScope));
            }

            ScopeActivated?.Invoke(this, scopeOpenedArgs);
            return scope;
        }

        public void Close(Scope scope)
        {
            var current = _activeScope.Get();
            if (current != null && current == scope)
            {
                // if the scope that was just closed was the active scope,
                // set its parent as the new active scope
                _activeScope.Set(current.Parent);
                ScopeDeactivated?.Invoke(this, new ScopeEventArgs(current));

                if (current.Parent != null)
                {
                    ScopeActivated?.Invoke(this, new ScopeEventArgs(current.Parent));
                }
            }

            ScopeClosed?.Invoke(this, new ScopeEventArgs(scope));
        }
    }
}
