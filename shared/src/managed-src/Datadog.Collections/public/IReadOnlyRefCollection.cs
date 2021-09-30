using System;

namespace Datadog.Collections
{
    public interface IReadOnlyRefCollection<T> : IRefEnumerable<T>
    {
        int Count { get; }
    }
}
