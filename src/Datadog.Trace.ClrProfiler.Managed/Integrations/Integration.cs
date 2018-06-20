using System;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The base class inherited by all integrations.
    /// </summary>
    public abstract class Integration : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Integration"/> class.
        /// </summary>
        protected Integration()
        {
            // TODO: explicitly set upstream Scope as parent for this new Scope, but Span.Context is currently internal
            Scope = Tracer.Instance.StartActive(string.Empty);
        }

        /// <summary>
        /// Gets a value indicating whether this integration is enabled.
        /// Defaults to <c>true</c> unless explicitly disabled.
        /// </summary>
        // TODO: read from configuration
        public virtual bool IsEnabled { get; } = true;

        /// <summary>
        /// Gets the <see cref="Scope"/> created by this integration instance.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        // [System.Security.SecuritySafeCritical]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> is called from <see cref="Dispose()"/>; <c>false</c> otherwise.</param>
        // [System.Security.SecuritySafeCritical]
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Scope?.Dispose();
            }
        }
    }
}
