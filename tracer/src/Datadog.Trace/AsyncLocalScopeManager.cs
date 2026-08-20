// <copyright file="AsyncLocalScopeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.LibDatadog.OtelThreadContext;

namespace Datadog.Trace
{
    internal sealed class AsyncLocalScopeManager : IScopeManager, IScopeRawAccess
    {
        private readonly IContextTracker _contextTracker;
        private readonly bool _primaryScopeChangeNotificationsEnabled;
        private readonly AsyncLocal<Scope> _activeScope;
        private IOtelThreadContextPublisher _otelThreadContextPublisher;
        private AsyncLocal<Scope> _otelNotificationScope;

        public AsyncLocalScopeManager()
            : this(Profiler.Instance.ContextTracker, OtelThreadContextPublisher.Disabled)
        {
        }

        internal AsyncLocalScopeManager(IContextTracker contextTracker, IOtelThreadContextPublisher otelThreadContextPublisher)
        {
            _contextTracker = contextTracker;
            _otelThreadContextPublisher = otelThreadContextPublisher;
            _primaryScopeChangeNotificationsEnabled = contextTracker.IsEnabled || otelThreadContextPublisher.IsEnabled;
            _activeScope = CreateScope();
        }

        public Scope Active
        {
            get => _activeScope.Value;
            private set
            {
                _activeScope.Value = value;
                SyncOtelNotificationScope(value);
            }
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

        private AsyncLocal<Scope> CreateScope()
        {
            if (_primaryScopeChangeNotificationsEnabled)
            {
                return new AsyncLocal<Scope>(OnScopeChanged);
            }

            return new AsyncLocal<Scope>();
        }

        internal void UpdateOtelThreadContextPublisher(IOtelThreadContextPublisher publisher)
        {
            var previousPublisher = Volatile.Read(ref _otelThreadContextPublisher);
            if (!publisher.IsEnabled)
            {
                var resetByNotificationScope = !_primaryScopeChangeNotificationsEnabled && SyncOtelNotificationScope(scope: null);
                if (!resetByNotificationScope)
                {
                    previousPublisher.Reset();
                }

                Volatile.Write(ref _otelThreadContextPublisher, publisher);
                return;
            }

            Volatile.Write(ref _otelThreadContextPublisher, publisher);
            if (_primaryScopeChangeNotificationsEnabled)
            {
                PublishOtelScope(Active, publisher);
                return;
            }

            EnsureOtelNotificationScope();
            var active = Active;
            var publishedByNotificationScope = SyncOtelNotificationScope(active);
            if (!publishedByNotificationScope && active != null)
            {
                publisher.Set(active.Span);
            }
        }

        private void OnScopeChanged(AsyncLocalValueChangedArgs<Scope> obj)
        {
            if (obj.CurrentValue == null)
            {
                _contextTracker.Reset();
                Volatile.Read(ref _otelThreadContextPublisher).Reset();
            }
            else
            {
                var span = obj.CurrentValue.Span;
                _contextTracker.Set(span.RootSpanId, span.SpanId);
                Volatile.Read(ref _otelThreadContextPublisher).Set(span);
            }
        }

        private void OnOtelScopeChanged(AsyncLocalValueChangedArgs<Scope> obj)
        {
            PublishOtelScope(obj.CurrentValue, Volatile.Read(ref _otelThreadContextPublisher));
        }

        private void EnsureOtelNotificationScope()
        {
            if (Volatile.Read(ref _otelNotificationScope) == null)
            {
                Volatile.Write(ref _otelNotificationScope, new AsyncLocal<Scope>(OnOtelScopeChanged));
            }
        }

        private bool SyncOtelNotificationScope(Scope scope)
        {
            var notificationScope = Volatile.Read(ref _otelNotificationScope);
            if (notificationScope != null && notificationScope.Value != scope)
            {
                notificationScope.Value = scope;
                return true;
            }

            return false;
        }

        private void PublishOtelScope(Scope scope, IOtelThreadContextPublisher publisher)
        {
            if (scope == null)
            {
                publisher.Reset();
            }
            else
            {
                publisher.Set(scope.Span);
            }
        }
    }
}
