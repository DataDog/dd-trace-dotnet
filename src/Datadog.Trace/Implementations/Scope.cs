using System;

namespace Datadog.Trace
{
    public class Scope : IDisposable
    {
        private readonly AsyncLocalScopeManager _scopeManager;
        private bool _finishOnClose;

        internal Scope(Scope parent, SpanBase span,  AsyncLocalScopeManager scopeManager, bool finishOnClose)
        {
            Parent = parent;
            Span = span;
            _scopeManager = scopeManager;
            _finishOnClose = finishOnClose;
        }

        public SpanBase Span { get; }

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