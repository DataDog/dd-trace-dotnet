using Datadog.Util;
using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public static class DiagnosticSourceAssembly
    {
        public interface IDynamicInvoker
        {
            bool IsValid { get; }
            string DiagnosticSourceAssemblyName { get; }
            IDisposable SubscribeInvalidatedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker> invokerInvalidatedAction);
            IDisposable SubscribeInvalidatedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker, object> invokerInvalidatedAction, object state);
        }

        public static bool EnsureInitialized()
        {
            return DynamicLoader.EnsureInitialized();
        }

        public static bool IsInitialized
        {
            get
            {
                return (DynamicLoader.InitializationState == DynamicLoader.InitState.Initialized)
                            && DynamicInvoker.TryGetCurrent(out DynamicInvoker invoker)
                            && invoker.IsValid;
            }
        }

        public static IDisposable SubscribeDynamicInvokerInitializedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker> dynamicInvokerInitializedAction)
        {
            Validate.NotNull(dynamicInvokerInitializedAction, nameof(dynamicInvokerInitializedAction));

            return SubscribeDynamicInvokerInitializedListener((invoker, _) => dynamicInvokerInitializedAction(invoker), state: null);
        }

        public static IDisposable SubscribeDynamicInvokerInitializedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker, object> dynamicInvokerInitializedAction, object state)
        {
            Validate.NotNull(dynamicInvokerInitializedAction, nameof(dynamicInvokerInitializedAction));

            return DynamicInvoker.SubscribeInitializedListener(dynamicInvokerInitializedAction, state);
        }
    }
}
