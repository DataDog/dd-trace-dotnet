// <copyright file="Span.ISpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// A Span represents a logical unit of work in the system. It may be
    /// related to other spans by parent/children relationships. The span
    /// tracks the duration of an operation as well as associated metadata in
    /// the form of a resource name, a service name, and user defined tags.
    /// </summary>
    public partial class Span : ISpan
    {
        /// <summary>
        /// Gets or sets operation name
        /// </summary>
        string ISpan.OperationName
        {
            get => OperationName;
            set => OperationName = value;
        }

        /// <summary>
        /// Gets or sets the resource name
        /// </summary>
        string ISpan.ResourceName
        {
            get => ResourceName;
            set => ResourceName = value;
        }

        /// <summary>
        /// Gets or sets the type of request this span represents (ex: web, db).
        /// Not to be confused with span kind.
        /// </summary>
        /// <seealso cref="SpanTypes"/>
        /// <summary>
        /// Gets or sets the resource name
        /// </summary>
        string ISpan.Type
        {
            get => Type;
            set => Type = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this span represents an error
        /// </summary>
        bool ISpan.Error
        {
            get => Error;
            set => Error = value;
        }

        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        string ISpan.ServiceName
        {
            get => ServiceName;
            set => ServiceName = value;
        }

        /// <summary>
        /// Gets the trace's unique identifier.
        /// </summary>
        ulong ISpan.TraceId => TraceId;

        /// <summary>
        /// Gets the span's unique identifier.
        /// </summary>
        ulong ISpan.SpanId => SpanId;

        /// <summary>
        /// Gets the span's span context
        /// </summary>
        ISpanContext ISpan.Context => Context;

        /// <summary>
        /// Add a the specified tag to this span.
        /// </summary>
        /// <param name="key">The tag's key.</param>
        /// <param name="value">The tag's value.</param>
        /// <returns>This span to allow method chaining.</returns>
        ISpan ISpan.SetTag(string key, string value) => SetTag(key, value);

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        void ISpan.Finish() => Finish();

        /// <summary>
        /// Explicitly set the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        /// <param name="finishTimestamp">Explicit value for the end time of the Span</param>
        void ISpan.Finish(DateTimeOffset finishTimestamp) => Finish(finishTimestamp);

        /// <summary>
        /// Add the StackTrace and other exception metadata to the span
        /// </summary>
        /// <param name="exception">The exception.</param>
        void ISpan.SetException(Exception exception) => SetException(exception);

        /// <summary>
        /// Gets the value (or default/null if the key is not a valid tag) of a tag with the key value passed
        /// </summary>
        /// <param name="key">The tag's key</param>
        /// <returns> The value for the tag with the key specified, or null if the tag does not exist</returns>
        string ISpan.GetTag(string key) => GetTag(key);
    }
}
