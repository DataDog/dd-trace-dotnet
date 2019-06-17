using System;

namespace Datadog.Trace
{
    internal interface IScopeManager
    {
        event EventHandler<ScopeEventArgs> ScopeOpened;

        event EventHandler<ScopeEventArgs> ScopeActivated;

        event EventHandler<ScopeEventArgs> ScopeDeactivated;

        event EventHandler<ScopeEventArgs> ScopeClosed;

        Scope Active { get; }

        Scope Activate(Span span, bool finishOnClose);

        void Close(Scope scope);
    }
}
