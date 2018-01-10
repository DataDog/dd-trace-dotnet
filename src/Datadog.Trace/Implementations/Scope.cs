using System;

namespace Datadog.Trace
{
    public class Scope : IDisposable
    {
        private readonly AsyncLocalScopeManager _scopeManager;
        private bool _finishOnClose;

        internal Scope(Scope parent, Span span,  AsyncLocalScopeManager scopeManager, bool finishOnClose)
        {
            Parent = parent;
            Span = span;
            _scopeManager = scopeManager;
            _finishOnClose = finishOnClose;
        }

        public Span Span { get; }

        internal Scope Parent { get; }

        public void Dispose()
        {
            _scopeManager.Close(this);
            if (_finishOnClose)
            {
                Span.Finish();
            }
        }
    }
}