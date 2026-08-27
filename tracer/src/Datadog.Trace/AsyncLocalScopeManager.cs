// <copyright file="AsyncLocalScopeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.OtelThreadContext;

namespace Datadog.Trace
{
    internal sealed class AsyncLocalScopeManager : IScopeManager, IScopeRawAccess
    {
        // Consumers notified whenever the active scope changes on the current thread. Both the Continuous
        // Profiler and the OpenTelemetry thread context need the context of the OS thread rather than of
        // the logical call context, which is why they are driven from the AsyncLocal change callback: it
        // fires on the thread performing the ExecutionContext restore, including async continuations and
        // thread-pool hand-offs.
        //
        // Note these live in a single AsyncLocal callback on purpose. Registering a second AsyncLocal to
        // observe the same value would double the cost of every ExecutionContext restore.
        private readonly IOtelThreadContextPublisher _otelThreadContextPublisher;
        private readonly AsyncLocal<Scope> _activeScope;

        public AsyncLocalScopeManager()
            : this(NullOtelThreadContextPublisher.Instance)
        {
        }

        internal AsyncLocalScopeManager(IOtelThreadContextPublisher otelThreadContextPublisher)
        {
            _otelThreadContextPublisher = otelThreadContextPublisher;
            _activeScope = CreateScope(otelThreadContextPublisher);
        }

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
            DistributedTracer.Instance.SetSpanContext(scope.Span.Context.Parent as SpanContext);
        }

        private AsyncLocal<Scope> CreateScope(IOtelThreadContextPublisher otelThreadContextPublisher)
        {
            if (Profiler.Instance.ContextTracker.IsEnabled || otelThreadContextPublisher.IsEnabled)
            {
                return new AsyncLocal<Scope>(OnScopeChanged);
            }

            return new AsyncLocal<Scope>();
        }

        private void OnScopeChanged(AsyncLocalValueChangedArgs<Scope> obj)
        {
            if (obj.CurrentValue == null)
            {
                Profiler.Instance.ContextTracker.Reset();
                _otelThreadContextPublisher.Reset();
            }
            else
            {
                var span = obj.CurrentValue.Span;
                Profiler.Instance.ContextTracker.Set(span.RootSpanId, span.SpanId);
                _otelThreadContextPublisher.Set(span);
            }
        }
    }
}
