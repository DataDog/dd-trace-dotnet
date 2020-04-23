using System;

namespace Datadog.Trace
{
    internal interface IDatadogTracer
    {
        string DefaultServiceName { get; }

        IScopeManager ScopeManager { get; }

        Span StartSpan(string operationName);

        Span StartSpan(string operationName, ISpanContext parent);

        Span StartSpan(string operationName, ISpanContext parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope);

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

        double? GetIntegrationAnalyticsSampleRate(string name, bool enabledWithGlobalSetting);
    }
}
