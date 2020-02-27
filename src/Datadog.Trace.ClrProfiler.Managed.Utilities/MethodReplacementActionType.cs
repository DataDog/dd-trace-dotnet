namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Enum that instructs the CLR profiler where to insert the method call
    /// in the caller's method body.
    /// </summary>
    public enum MethodReplacementActionType
    {
        /// <summary>
        /// The new method call should replace the original method call in the
        /// caller's body.
        /// </summary>
        ReplaceTargetMethod,

        /// <summary>
        /// The new method call should be placed at the beginning of the caller's body.
        /// </summary>
        InsertFirst,

        /// <summary>
        /// The new method call should be placed at the end of the caller's body.
        /// </summary>
        InsertLast
    }
}
