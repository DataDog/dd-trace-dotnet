namespace Datadog.Trace
{
#if NET45
    using System.Runtime.Remoting.Messaging;

    // TODO:bertrand revisit this when we want to support multiple AppDomains
    internal class AsyncLocalCompat<T>
    {
        private string _name;

        public AsyncLocalCompat(string name)
        {
            _name = name;
        }

        public T Get()
        {
            return (T)CallContext.LogicalGetData(_name);
        }

        public void Set(T value)
        {
            CallContext.LogicalSetData(_name, value);
        }
    }

#else
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    internal class AsyncLocalCompat<T>
    {
        private static readonly ConcurrentDictionary<string, AsyncLocal<T>> AsyncLocalByName
            = new ConcurrentDictionary<string, AsyncLocal<T>>();

        private static readonly Func<string, AsyncLocal<T>> Create = name => new AsyncLocal<T>();

        private AsyncLocal<T> _asyncLocal;

        public AsyncLocalCompat(string name)
        {
            _asyncLocal = AsyncLocalByName.GetOrAdd(name, Create);
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