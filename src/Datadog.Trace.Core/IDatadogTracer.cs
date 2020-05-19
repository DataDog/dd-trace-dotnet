using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Interface for tracer instances
    /// </summary>
    public interface IDatadogTracer
    {
        /// <summary>
        /// Gets the default service name for this process
        /// </summary>
        string DefaultServiceName { get; }

        /// <summary>
        /// Gets the scope manager for this tracer.
        /// </summary>
        IScopeManager ScopeManager { get; }

        /// <summary>
        /// Starts a span for the context of this tracer.
        /// </summary>
        /// <param name="operationName">The type of operation</param>
        /// <returns>The span</returns>
        Span StartSpan(string operationName);

        /// <summary>
        /// Starts a span for the context of this tracer.
        /// </summary>
        /// <param name="operationName">The type of operation</param>
        /// <param name="parent">The span parent</param>
        /// <returns>The span</returns>
        Span StartSpan(string operationName, ISpanContext parent);

        /// <summary>
        /// Starts a span for the context of this tracer
        /// </summary>
        /// <param name="operationName">The type of operation</param>
        /// <param name="parent">The parent</param>
        /// <param name="serviceName">The service</param>
        /// <param name="startTime">When it was started</param>
        /// <param name="ignoreActiveScope">Whether it should be standalone</param>
        /// <returns>The span</returns>
        Span StartSpan(string operationName, ISpanContext parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope);

        /// <summary>
        /// Spans to write to the agent.
        /// </summary>
        /// <param name="span">Array of spans</param>
        void Write(Span[] span);

        /// <summary>
        /// Make a span the active span and return its new scope.
        /// </summary>
        /// <param name="span">The span to activate.</param>
        /// <returns>A Scope object wrapping this span.</returns>
        Scope ActivateSpan(Span span);

        /// <summary>
        /// Make a span the active span and return its new scope.
        /// </summary>
        /// <param name="span">The span to activate.</param>
        /// <param name="finishOnClose">Determines whether closing the returned scope will also finish the span.</param>
        /// <returns>A Scope object wrapping this span.</returns>
        Scope ActivateSpan(Span span, bool finishOnClose);

        /// <summary>
        /// The sample rate for this integration.
        /// </summary>
        /// <param name="name">The integration</param>
        /// <param name="enabledWithGlobalSetting">Whether the integration is enabled</param>
        /// <returns>The sample rate</returns>
        double? GetIntegrationAnalyticsSampleRate(string name, bool enabledWithGlobalSetting);
    }
}
