namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// A list of values that indicate the built-in integrations supported by Datadog's profiler.
    /// </summary>
    public enum IntegrationType
    {
        /// <summary>
        /// Default value that indicates that integration type was not specified.
        /// </summary>
        None = 0,

        /// <summary>
        /// A custom integration that instruments methods indicated through configuration.
        /// </summary>
        Custom = 1,

        /// <summary>
        /// Automatic instrumentation for ASP.NET MVC 5.
        /// </summary>
        AspNetMvc5 = 2,

        /// <summary>
        /// Automatic instrumentation for ASP.NET Core MVC 2.
        /// </summary>
        AspNetCoreMvc2 = 3,
    }
}
