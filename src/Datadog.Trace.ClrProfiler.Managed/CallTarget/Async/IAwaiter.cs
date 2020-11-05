using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.CallTarget.Async
{
    /// <summary>
    /// IAwaiter interface
    /// </summary>
    public interface IAwaiter : ICriticalNotifyCompletion
    {
        /// <summary>
        /// Gets a value indicating whether the await task is completed
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets the awaiter result
        /// </summary>
        void GetResult();
    }

    /// <summary>
    /// IAwaiter interface
    /// </summary>
    /// <typeparam name="TResult">Result type of the await</typeparam>
    public interface IAwaiter<TResult> : ICriticalNotifyCompletion
    {
        /// <summary>
        /// Gets a value indicating whether the await task is completed
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets the awaiter result
        /// </summary>
        /// <returns>Result of the awaited task</returns>
        TResult GetResult();
    }
}
