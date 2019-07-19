using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class ScopeManager : IScopeManager
    {
        private static readonly ILog Log = LogProvider.For<ScopeManager>();

        private readonly AsyncLocalCompatScopeAccess _defaultScopeAccess = new AsyncLocalCompatScopeAccess();
        private readonly object _activeScopeLock = new object();
        private readonly object _scopingLock = new object();

        private List<IActiveScopeAccess> _prioritizedAmbientAccess = new List<IActiveScopeAccess>();

        public event EventHandler<SpanEventArgs> SpanActivated;

        public event EventHandler<SpanEventArgs> SpanClosed;

        public event EventHandler<SpanEventArgs> TraceEnded;

        public Scope Active
        {
            get
            {
                lock (_activeScopeLock)
                {
                    for (int i = 0; i < _prioritizedAmbientAccess.Count; i++)
                    {
                        try
                        {
                            var activeScope = _prioritizedAmbientAccess[i].GetActiveScope();
                            if (activeScope != null)
                            {
                                return activeScope;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorException($"Active scope access failed: {_prioritizedAmbientAccess[i]?.GetType().FullName}.", ex);
                        }
                    }

                    // if all else fails, this is the one
                    return _defaultScopeAccess.GetActiveScope();
                }
            }
        }

        public void SetActiveScope(Scope scope)
        {
            lock (_activeScopeLock)
            {
                for (int i = 0; i < _prioritizedAmbientAccess.Count; i++)
                {
                    try
                    {
                        _prioritizedAmbientAccess[i].TrySetActiveScope(scope);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException($"Active scope set failed: {_prioritizedAmbientAccess[i]?.GetType().FullName}.", ex);
                    }
                }

                // if all else fails, this is the one
                _defaultScopeAccess.TrySetActiveScope(scope);
            }
        }

        public Scope Activate(Span span, bool finishOnClose)
        {
            lock (_scopingLock)
            {
                var newParent = Active;
                var scope = new Scope(newParent, span, this, finishOnClose);
                var scopeOpenedArgs = new SpanEventArgs(span);

                SetActiveScope(scope);

                SpanActivated?.Invoke(this, scopeOpenedArgs);

                return scope;
            }
        }

        public void Close(Scope scope)
        {
            lock (_scopingLock)
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
        }

        public void DeRegisterScopeAccess(IActiveScopeAccess scopeAccess)
        {
            lock (_activeScopeLock)
            {
                _prioritizedAmbientAccess =
                    _prioritizedAmbientAccess
                       .Where(psa => psa != scopeAccess)
                       .OrderByDescending(i => i.Priority)
                       .ToList();
            }
        }

        public void RegisterScopeAccess(IActiveScopeAccess scopeAccess)
        {
            lock (_activeScopeLock)
            {
                if (_prioritizedAmbientAccess.Any(i => i.GetType() == scopeAccess.GetType()))
                {
                    return;
                }

                // TODO: will we ever need this?
                // _prioritizedAmbientAccess.Add(scopeAccess);

                // Slower add for faster read
                _prioritizedAmbientAccess =
                    _prioritizedAmbientAccess
                       .OrderByDescending(i => i.Priority)
                       .ThenByDescending(i => i.CreatedAtTicks)
                       .ToList();
            }
        }
    }
}
