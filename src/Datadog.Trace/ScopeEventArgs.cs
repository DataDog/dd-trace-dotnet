using System;

namespace Datadog.Trace
{
    internal class ScopeEventArgs : EventArgs
    {
        public ScopeEventArgs(Scope scope)
        {
            Scope = scope;
        }

        internal Scope Scope { get; }
    }
}
