using System;
using System.Threading;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvokerHandle<T> : IDynamicInvokerHandle
        where T : class
    {
        private T _dynamicInvoker;

        internal DynamicInvokerHandle(T dynamicInvoker)
        {
            Volatile.Write(ref _dynamicInvoker, dynamicInvoker);
        }

        internal void Invalidate()
        { 
            Volatile.Write(ref _dynamicInvoker, null);
        }

        public bool TryGetInvoker(out T dynamicInvoker)
        {
            dynamicInvoker = Volatile.Read(ref _dynamicInvoker);
            return (dynamicInvoker != null);
        }

        public bool IsValid
        {
            get { return (Volatile.Read(ref _dynamicInvoker) != null); }
        }
    }
}
