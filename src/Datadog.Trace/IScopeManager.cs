using System;

namespace Datadog.Trace
{
    internal interface IScopeManager
    {
        event EventHandler<SpanEventArgs> SpanOpened;

        event EventHandler<SpanEventArgs> SpanActivated;

        event EventHandler<SpanEventArgs> SpanDeactivated;

        event EventHandler<SpanEventArgs> SpanClosed;

        event EventHandler<SpanEventArgs> TraceEnded;

        Scope Active { get; }

        Scope Activate(Span span, bool finishOnClose);

        void Close(Scope scope);

        void RegisterScopeAccess(IAmbientContextAccess scopeAccess);

        void DeRegisterScopeAccess(IAmbientContextAccess scopeAccess);
    }
}
