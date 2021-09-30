using System;

namespace Datadog.Collections
{
    internal interface IReadOnlyRefCollectionInternal<T> : IRefEnumerableInternal<T>
    {
        int Count { get; }
    }
}
