// <copyright file="ScopeManagerBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal abstract class ScopeManagerBase : IScopeManager, IScopeRawAccess
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ScopeManagerBase));

        public event EventHandler<SpanEventArgs> TraceStarted;

        public event EventHandler<SpanEventArgs> SpanOpened;

        public event EventHandler<SpanEventArgs> SpanActivated;

        public event EventHandler<SpanEventArgs> SpanDeactivated;

        public event EventHandler<SpanEventArgs> SpanClosed;

        public event EventHandler<SpanEventArgs> TraceEnded;

        public abstract Scope Active { get; protected set; }

        Scope IScopeRawAccess.Active
        {
            get => Active;
            set => Active = value;
        }

        public Scope Activate(Span span, bool finishOnClose)
        {
            var newParent = Active;
            var scope = new Scope(newParent, span, this, finishOnClose);
            var scopeOpenedArgs = new SpanEventArgs(span);

            if (newParent == null)
            {
                TraceStarted?.Invoke(this, scopeOpenedArgs);
            }

            SpanOpened?.Invoke(this, scopeOpenedArgs);

            Active = scope;

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
                SpanClosed?.Invoke(this, new SpanEventArgs(scope.Span));
                return;
            }

            // if the scope that was just closed was the active scope,
            // set its parent as the new active scope
            Active = scope.Parent;
            SpanDeactivated?.Invoke(this, new SpanEventArgs(scope.Span));

            if (!isRootSpan)
            {
                SpanActivated?.Invoke(this, new SpanEventArgs(scope.Parent.Span));
            }

            SpanClosed?.Invoke(this, new SpanEventArgs(scope.Span));

            if (isRootSpan)
            {
                TraceEnded?.Invoke(this, new SpanEventArgs(scope.Span));
            }
        }
    }
}
