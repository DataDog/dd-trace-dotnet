namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// A list of values that indicate the built-in integrations supported by Datadog's profiler.
    /// </summary>
    public enum IntegrationType
    {
        /// <summary>
        /// A custom integration that instruments methods indicated through configuration.
        /// </summary>
        Custom = 0,

        /// <summary>
        /// Automatic instrumentation for ASP.NET MVC 5.
        /// </summary>
        AspNetMvc5 = 1,
    }
}
