namespace Datadog.Trace.Interfaces
{
    /// <summary>
    ///     Used for generating ID values used throughout the library (i.e. Spans, Traces, etc.)
    /// </summary>
    internal interface IIdProvider
    {
        /// <summary>
        ///     Generates a positive 64bit integer id value
        /// </summary>
        /// <returns>The positive 64bit integer value for use</returns>
        ulong GetUInt63Id();
    }
}
