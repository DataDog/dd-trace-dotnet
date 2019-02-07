namespace Datadog.Trace.Propagators
{
    /// <summary>
    /// Interface for objects that can propagate a <see cref="SpanContext"/>
    /// by injecting it into or extracting from a "wire" protocol, like HTTP.
    /// </summary>
    public interface IPropagator
    {
        /// <summary>
        /// Inject the specified <see cref="SpanContext"/>.
        /// </summary>
        /// <param name="context">The span context to inject.</param>
        void Inject(SpanContext context);

        /// <summary>
        /// Try to extract a <see cref="SpanContext"/>. Returns null if extraction fails.
        /// </summary>
        /// <returns>The extracted <see cref="SpanContext"/>, or null if extraction fails.</returns>
        SpanContext Extract();
    }
}
