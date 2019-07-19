using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Immutables
{
    internal interface IDatadogImmutableStack<T> : IEnumerable<T>, IEnumerable
    {
        bool IsEmpty { get; }

        IDatadogImmutableStack<T> Clear();

        IDatadogImmutableStack<T> Push(T value);

        IDatadogImmutableStack<T> Pop();

        T Peek();
    }
}