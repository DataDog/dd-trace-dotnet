namespace Datadog.Trace.ClrProfiler.CallTarget.Async
{
    /// <summary>
    /// IAwaitable interface
    /// </summary>
    public interface IAwaitable
    {
        /// <summary>
        /// Gets the awaiter interface
        /// </summary>
        /// <returns>Awaiter interface</returns>
        IAwaiter GetAwaiter();
    }

    /// <summary>
    /// IAwaitable interface
    /// </summary>
    /// <typeparam name="TResult">Result type of the await</typeparam>
    public interface IAwaitable<TResult>
    {
        /// <summary>
        /// Gets the awaiter interface
        /// </summary>
        /// <returns>Awaiter interface</returns>
        IAwaiter<TResult> GetAwaiter();
    }
}
