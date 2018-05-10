namespace Datadog.Trace
{
    /// <summary>
    /// This defines the interface that must be implemented by a header collection
    /// to support context Injection/Extraction.
    /// </summary>
    public interface IHeaderCollection
    {
        /// <summary>
        /// Get the value of the header corresponding to the key (it should be case insensitive)
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <returns>The header value.</returns>
        string Get(string name);

        /// <summary>
        /// Sets the value of the specified header.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        void Set(string name, string value);
    }
}
