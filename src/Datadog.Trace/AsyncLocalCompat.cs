namespace Datadog.Trace
{
#if NET45
    using System;
    using System.Runtime.Remoting;
    using System.Runtime.Remoting.Messaging;

    internal class AsyncLocalCompat<T>
    {
        private readonly string _name = "__Datadog_Scope_Current__" + Guid.NewGuid();

        public T Get()
        {
            var handle = CallContext.LogicalGetData(_name) as ObjectHandle;
            return handle == null ? default(T) : (T)handle.Unwrap();
        }

        public void Set(T value)
        {
            CallContext.LogicalSetData(_name, new ObjectHandle(value));
        }
    }

#else
    using System.Threading;

    internal class AsyncLocalCompat<T>
    {
        private AsyncLocal<T> _asyncLocal = new AsyncLocal<T>();

        public AsyncLocalCompat()
        {
        }

        public T Get()
        {
            return _asyncLocal.Value;
        }

        public void Set(T value)
        {
            _asyncLocal.Value = value;
        }
    }
#endif
}
