using System;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvokerInvalidationListenersCollection : ListenerActionsCollection<Action<DiagnosticSourceAssembly.IDynamicInvoker, object>,
                                                                                             DiagnosticSourceAssembly.IDynamicInvoker>
    {
        private readonly DiagnosticSourceAssembly.IDynamicInvoker _owner;

        public DynamicInvokerInvalidationListenersCollection(string logComponentMoniker, DiagnosticSourceAssembly.IDynamicInvoker owner)
            : base(logComponentMoniker ?? nameof(DynamicInvokerInvalidationListenersCollection))
        {
            Validate.NotNull(owner, nameof(owner));
            _owner = owner;
        }

        protected override void InvokeSubscription(Subscription subscription, DiagnosticSourceAssembly.IDynamicInvoker source)
        {
            subscription.Action(source, subscription.State);
        }

        protected override bool GetMustImmediatelyInvokeNewSubscription(Subscription subscription, out DiagnosticSourceAssembly.IDynamicInvoker source)
        {
            source = _owner;
            return !_owner.IsValid;
        }
    }
}
