using System.Collections.Generic;

namespace Datadog.Trace.Headers
{
    /// <summary>
    /// Specified a common interface that can be used to manipulate collections of headers.
    /// </summary>
    public interface IHeadersCollection
    {
        /// <summary>
        /// Returns all header values for a specified header stored in the collection.
        /// </summary>
        /// <param name="name">The specified header to return values for.</param>
        /// <returns>Zero or more header strings.</returns>
        IEnumerable<string> GetValues(string name);

        /// <summary>
        /// Sets the value of an entry in the collection, replacing any previous values.
        /// </summary>
        /// <param name="name">The header to add to the collection.</param>
        /// <param name="value">The content of the header.</param>
        void Set(string name, string value);

        /// <summary>
        /// Adds the specified header and its value into the collection.
        /// </summary>
        /// <param name="name">The header to add to the collection.</param>
        /// <param name="value">The content of the header.</param>
        void Add(string name, string value);

        /// <summary>
        /// Removes the specified header from the collection.
        /// </summary>
        /// <param name="name">The name of the header to remove from the collection.</param>
        void Remove(string name);
    }
}
