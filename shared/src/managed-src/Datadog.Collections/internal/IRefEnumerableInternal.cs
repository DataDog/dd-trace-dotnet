using System;

namespace Datadog.Collections
{
    /// <summary>
    /// Allows using pattern-based foreach ref enumerations on types that implemenet this interface.
    /// E.g.:
    /// <code>
    ///     internal struct InfoItem
    ///     {
    ///         ...
    ///     }
    ///     
    ///     internal class SomeCollection : IRefEnumerableInternal{InfoItem}
    ///     {
    ///         ...
    ///     }
    ///     
    ///     . . .
    ///     
    ///     SomeCollection data = GetData();
    ///     
    ///     foreach (ref InfoItem item in data)
    ///     {
    ///         ProcessItem(item);
    ///     }
    ///     
    ///     . . .
    ///     
    ///     void ProcessItem(ref InfoItem item)
    ///     {
    ///         ...
    ///     }
    /// </code>
    /// </summary>
    internal interface IRefEnumerableInternal<T>
    {
        IRefEnumeratorInternal<T> GetEnumerator();
    }
}
