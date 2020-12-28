namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Enum that instructs the CLR profiler, during JIT compilation of a method,
    /// where to insert a method call to the intercept method.
    /// </summary>
    public enum MethodReplacementActionType
    {
        /// <summary>
        /// All method calls to the target method should be replaced with method
        /// calls to the intercept method.
        /// This is the historical behavior of the CLR profiler and it requires
        /// that the method body of the intercept method invokes the original
        /// target method.
        /// </summary>
        ReplaceTargetMethod,

        /// <summary>
        /// The method call to the intercept method should be inserted at the
        /// beginning of the caller's method body.
        /// This action is not intended for generating spans. This should only
        /// be used for inserting profiler-initialization logic such as
        /// adding ASP.NET middleware.
        /// </summary>
        InsertFirst,

        /// <summary>
        /// The target method gets modified with two calls, the first one at the
        /// begining of the method body, and then at the end before returning the
        /// control to the caller.
        /// </summary>
        CallTargetModification,
    }
}
