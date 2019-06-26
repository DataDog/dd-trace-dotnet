using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager : IScopeManager
    {
        private static readonly ILog Log = LogProvider.For<AsyncLocalScopeManager>();

        private readonly AsyncLocalCompat<Scope> _activeScope = new AsyncLocalCompat<Scope>();

        public event EventHandler<SpanEventArgs> SpanOpened;

        public event EventHandler<SpanEventArgs> SpanActivated;

        public event EventHandler<SpanEventArgs> SpanDeactivated;

        public event EventHandler<SpanEventArgs> SpanClosed;

        public event EventHandler<SpanEventArgs> TraceEnded;

        public Scope Active => _activeScope.Get();

        public Scope Activate(Span span, bool finishOnClose)
        {
            var newParent = Active;
            var scope = new Scope(newParent, span, this, finishOnClose);
            var scopeOpenedArgs = new SpanEventArgs(span);

            SpanOpened?.Invoke(this, scopeOpenedArgs);

            _activeScope.Set(scope);

            if (scope.Parent != null)
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

            if (current != null && current == scope)
            {
                // if the scope that was just closed was the active scope,
                // set its parent as the new active scope
                _activeScope.Set(current.Parent);
                SpanDeactivated?.Invoke(this, new SpanEventArgs(current.Span));

                if (!isRootSpan)
                {
                    SpanActivated?.Invoke(this, new SpanEventArgs(current.Parent.Span));
                }
            }

            if (current != null)
            {
                SpanClosed?.Invoke(this, new SpanEventArgs(scope.Span));

                if (isRootSpan)
                {
                    TraceEnded?.Invoke(this, new SpanEventArgs(scope.Span));
                }
            }
        }
    }
}
