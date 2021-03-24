namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Semantic convention used to when defining operation names, span tags, statuses etc.
    /// </summary>
    public enum ConventionType
    {
        /// <summary>
        /// The default convention. Currently it is Datadog (sic!).
        /// </summary>
        Default,

        /// <summary>
        /// The OpenTelemetry convention.
        /// </summary>
        OpenTelemetry,

        /// <summary>
        /// The Datadog convention. Current default.
        /// </summary>
        Datadog,
    }
}