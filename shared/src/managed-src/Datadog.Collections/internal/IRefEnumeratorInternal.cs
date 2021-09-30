using System;

namespace Datadog.Collections
{
    /// <summary>
    /// Supports <see cref="IRefEnumerableInternal{T}" />.
    /// </summary>
    internal interface IRefEnumeratorInternal<T> : IDisposable
    {
        ref T Current { get; }
        bool MoveNext();
        void Reset();
    }
}
