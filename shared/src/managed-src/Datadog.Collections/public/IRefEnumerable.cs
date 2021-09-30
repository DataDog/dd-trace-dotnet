using System;

namespace Datadog.Collections
{
    /// <summary>
    /// Allows using pattern-based foreach ref enumerations on types that implemenet this interface.
    /// E.g.:
    /// <code>
    ///     public struct InfoItem
    ///     {
    ///         ...
    ///     }
    ///     
    ///     public class SomeCollection : IRefEnumerable{InfoItem}
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
    /// </code>
    /// </summary>
    public interface IRefEnumerable<T>
    {
        IRefEnumerator<T> GetEnumerator();
    }
}
