using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class ScopeManager : IScopeManager
    {
        private static readonly ILog Log = LogProvider.For<ScopeManager>();

        private readonly object _scopeAccessLock = new object();
        private List<IActiveScopeAccess> _prioritizedScopeAccess = new List<IActiveScopeAccess>
        {
            new AsyncLocalCompatScopeAccess()
        };

        public event EventHandler<SpanEventArgs> SpanOpened;

        public event EventHandler<SpanEventArgs> SpanActivated;

        public event EventHandler<SpanEventArgs> SpanDeactivated;

        public event EventHandler<SpanEventArgs> SpanClosed;

        public event EventHandler<SpanEventArgs> TraceEnded;

        public Scope Active
        {
            get
            {
                lock (_scopeAccessLock)
                {
                    for (int i = 0; i < _prioritizedScopeAccess.Count; i++)
                    {
                        try
                        {
                            var activeScope = _prioritizedScopeAccess[i].GetActiveScope();
                            if (activeScope != null)
                            {
                                return activeScope;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorException($"Active scope access failed: {_prioritizedScopeAccess[i]?.GetType().FullName}.", ex);
                            continue;
                        }
                    }
                }

                return null;
            }
        }

        public Scope Activate(Span span, bool finishOnClose)
        {
            var newParent = Active;
            var scope = new Scope(newParent, span, this, finishOnClose);
            var scopeOpenedArgs = new SpanEventArgs(span);

            SpanOpened?.Invoke(this, scopeOpenedArgs);

            SetActiveScope(scope);

            if (newParent != null)
            {
                SpanDeactivated?.Invoke(this, new SpanEventArgs(newParent.Span));
            }

            SpanActivated?.Invoke(this, scopeOpenedArgs);

            return scope;
        }

        public void Close(Scope scope)
        {
            var current = Active;
            var isRootSpan = scope.Parent == null;

            if (current == null || current != scope)
            {
                // This is not the current scope for this context, bail out
                return;
            }

            // if the scope that was just closed was the active scope,
            // set its parent as the new active scope
            SetActiveScope(current.Parent);
            SpanDeactivated?.Invoke(this, new SpanEventArgs(current.Span));

            if (!isRootSpan)
            {
                SpanActivated?.Invoke(this, new SpanEventArgs(current.Parent.Span));
            }

            SpanClosed?.Invoke(this, new SpanEventArgs(scope.Span));

            if (isRootSpan)
            {
                TraceEnded?.Invoke(this, new SpanEventArgs(scope.Span));
            }
        }

        public void DeRegisterScopeAccess(IActiveScopeAccess scopeAccess)
        {
            lock (_scopeAccessLock)
            {
                _prioritizedScopeAccess =
                    _prioritizedScopeAccess
                       .Where(psa => psa != scopeAccess)
                       .OrderByDescending(i => i.Priority)
                       .ToList();
            }
        }

        public void RegisterScopeAccess(IActiveScopeAccess scopeAccess)
        {
            lock (_scopeAccessLock)
            {
                if (_prioritizedScopeAccess.Any(i => i.GetType() == scopeAccess.GetType()))
                {
                    // We don't want multiple instances as each type manages scope in an ambient fashion.
                    return;
                }

                _prioritizedScopeAccess.Add(scopeAccess);

                // Slower add for faster read
                _prioritizedScopeAccess = _prioritizedScopeAccess.OrderByDescending(i => i.Priority).ToList();
            }
        }

        private void SetActiveScope(Scope scope)
        {
            lock (_scopeAccessLock)
            {
                for (int i = 0; i < _prioritizedScopeAccess.Count; i++)
                {
                    try
                    {
                        if (_prioritizedScopeAccess[i].TrySetActiveScope(scope))
                        {
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException($"Active scope set failed: {_prioritizedScopeAccess[i]?.GetType().FullName}.", ex);
                    }
                }
            }
        }
    }
}
