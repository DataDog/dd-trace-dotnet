using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Interfaces.IPropertyBag
    /// </summary>
    public interface IPropertyBag
    {
        /// <summary>
        /// Gets a collection containing all the keys in the property set
        /// </summary>
        ICollection<string> Keys { get; }

        /// <summary>
        /// Gets or sets the list of values for a particular key
        /// </summary>
        /// <param name="key">The key for which the values are to be retrieved</param>
        IList this[string key] { get; }

        /// <summary>
        /// Gets a single value for a key, using the first
        /// one if multiple values are present and returning
        /// null if the value is not found.
        /// </summary>
        /// <param name="key">the key for which the values are to be retrieved</param>
        /// <returns>First value of the list for the key; otherwise null.</returns>
        object Get(string key);
    }
}
