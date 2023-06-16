// <copyright file="AsyncLocalScopeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager : IScopeManager, IScopeRawAccess
    {
        private readonly AsyncLocal<Scope> _activeScope = CreateScope();

        public Scope Active
        {
            get => _activeScope.Value;
            private set => _activeScope.Value = value;
        }

        Scope IScopeRawAccess.Active
        {
            get => Active;
            set => Active = value;
        }

        public Scope Activate(Span span, bool finishOnClose)
        {
            var newParent = Active;
            var scope = new Scope(newParent, span, this, finishOnClose);

            Active = scope;
            DistributedTracer.Instance.SetSpanContext(scope.Span.Context);

            return scope;
        }

        public void Close(Scope scope)
        {
            var current = Active;

            if (current == null || current != scope)
            {
                // This is not the current scope for this context, bail out
                return;
            }

            // if the scope that was just closed was the active scope,
            // set its parent as the new active scope
            Active = scope.Parent;

            // scope.Parent is null for distributed traces, so use scope.Span.Context.Parent
            DistributedTracer.Instance.SetSpanContext(scope.Span.Context.ParentInternal as SpanContext);
        }

        private static AsyncLocal<Scope> CreateScope()
        {
            if (Profiler.Instance.ContextTracker.IsEnabled)
            {
                return new AsyncLocal<Scope>(OnScopeChanged);
            }

            return new AsyncLocal<Scope>();
        }

        private static void OnScopeChanged(AsyncLocalValueChangedArgs<Scope> obj)
        {
            if (obj.CurrentValue == null)
            {
                Profiler.Instance.ContextTracker.Reset();
            }
            else
            {
                Profiler.Instance.ContextTracker.Set(obj.CurrentValue.Span.RootSpanId, obj.CurrentValue.Span.SpanId);
            }
        }
    }
}
