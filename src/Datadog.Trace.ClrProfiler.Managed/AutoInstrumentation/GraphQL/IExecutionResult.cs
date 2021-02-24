namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.ExecutionResult interface for ducktyping
    /// </summary>
    public interface IExecutionResult
    {
        /// <summary>
        /// Gets the execution errors
        /// </summary>
        IExecutionErrors Errors { get; }
    }
}
