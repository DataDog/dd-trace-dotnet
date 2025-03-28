// <copyright file="Span.ISpan.cs" company="Datadog">
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
    internal partial class Span : ISpan
    {
        /// <inheritdoc />
        string? ISpan.OperationName
        {
            get => OperationName;
            set => OperationName = value;
        }

        /// <inheritdoc />
        string? ISpan.ResourceName
        {
            get => ResourceName;
            set => ResourceName = value;
        }

        /// <inheritdoc />
        string? ISpan.Type
        {
            get => Type;
            set => Type = value;
        }

        /// <inheritdoc />
        bool ISpan.Error
        {
            get => Error;
            set => Error = value;
        }

        /// <inheritdoc />
        string? ISpan.ServiceName
        {
            get => ServiceName;
            set => ServiceName = value;
        }

        /// <inheritdoc />
        // this public API always returns the lower 64-bits, truncate using TraceId128.Lower
        ulong ISpan.TraceId => TraceId128.Lower;

        /// <inheritdoc />
        ulong ISpan.SpanId => SpanId;

        /// <inheritdoc />
        ISpanContext ISpan.Context => Context;

        /// <inheritdoc />
        ISpan ISpan.SetTag(string key, string? value) => SetTag(key, value);

        /// <inheritdoc />
        void ISpan.Finish() => Finish();

        /// <inheritdoc />
        void ISpan.Finish(DateTimeOffset finishTimestamp) => Finish(finishTimestamp);

        /// <inheritdoc />
        void ISpan.SetException(Exception exception) => SetException(exception);

        /// <inheritdoc />
        string ISpan.GetTag(string key) => GetTag(key);
    }
}
