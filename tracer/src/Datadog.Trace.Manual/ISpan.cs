// <copyright file="ISpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// A Span represents a logical unit of work in the system. It may be
    /// related to other spans by parent/children relationships. The span
    /// tracks the duration of an operation as well as associated metadata in
    /// the form of a resource name, a service name, and user defined tags.
    /// </summary>
    public interface ISpan : IDisposable
    {
        /// <summary>
        /// Gets or sets operation name
        /// </summary>
        string? OperationName { get; set; }

        /// <summary>
        /// Gets or sets the resource name
        /// </summary>
        string? ResourceName { get; set; }

        /// <summary>
        /// Gets or sets the type of request this span represents (ex: web, db).
        /// Not to be confused with span kind.
        /// </summary>
        /// <seealso cref="SpanTypes"/>
        string? Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this span represents an error
        /// </summary>
        bool Error { get; set; }

        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        string? ServiceName { get; set; }

        /// <summary>
        /// Gets the lower 64 bits of the trace's unique 128-bit identifier.
        /// </summary>
        ulong TraceId { get; }

        /// <summary>
        /// Gets the span's unique 64-bit identifier.
        /// </summary>
        ulong SpanId { get; }

        /// <summary>
        /// Gets the span's span context
        /// </summary>
        ISpanContext Context { get;  }

        /// <summary>
        /// Add a the specified tag to this span.
        /// </summary>
        /// <param name="key">The tag's key.</param>
        /// <param name="value">The tag's value.</param>
        /// <returns>This span to allow method chaining.</returns>
        ISpan SetTag(string key, string? value);

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        void Finish();

        /// <summary>
        /// Explicitly set the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        /// <param name="finishTimestamp">Explicit value for the end time of the Span</param>
        void Finish(DateTimeOffset finishTimestamp);

        /// <summary>
        /// Add the StackTrace and other exception metadata to the span
        /// </summary>
        /// <param name="exception">The exception.</param>
        void SetException(Exception exception);

        /// <summary>
        /// Gets the value (or default/null if the key is not a valid tag) of a tag with the key value passed
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <returns> The value for the tag with the key specified, or null if the tag does not exist</returns>
        string? GetTag(string key);
    }
}
