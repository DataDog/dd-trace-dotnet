// <copyright file="Span.ISpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Internal;

namespace Datadog.Trace
{
    /// <summary>
    /// A Span represents a logical unit of work in the system. It may be
    /// related to other spans by parent/children relationships. The span
    /// tracks the duration of an operation as well as associated metadata in
    /// the form of a resource name, a service name, and user defined tags.
    /// </summary>
    internal partial class Span : IInternalSpan
    {
        /// <inheritdoc />
        string? IInternalSpan.OperationName
        {
            get => OperationName;
            set => OperationName = value;
        }

        /// <inheritdoc />
        string? IInternalSpan.ResourceName
        {
            get => ResourceName;
            set => ResourceName = value;
        }

        /// <inheritdoc />
        string? IInternalSpan.Type
        {
            get => Type;
            set => Type = value;
        }

        /// <inheritdoc />
        bool IInternalSpan.Error
        {
            get => Error;
            set => Error = value;
        }

        /// <inheritdoc />
        string? IInternalSpan.ServiceName
        {
            get => ServiceName;
            set => ServiceName = value;
        }

        /// <inheritdoc />
        // this public API always returns the lower 64-bits, truncate using TraceId128.Lower
        ulong IInternalSpan.TraceId => TraceId128.Lower;

        /// <inheritdoc />
        ulong IInternalSpan.SpanId => SpanId;

        /// <inheritdoc />
        IInternalSpanContext IInternalSpan.Context => Context;

        /// <inheritdoc />
        IInternalSpan IInternalSpan.SetTag(string key, string? value) => SetTag(key, value);

        /// <inheritdoc />
        void IInternalSpan.Finish() => Finish();

        /// <inheritdoc />
        void IInternalSpan.Finish(DateTimeOffset finishTimestamp) => Finish(finishTimestamp);

        /// <inheritdoc />
        void IInternalSpan.SetException(Exception exception) => SetException(exception);

        /// <inheritdoc />
        string IInternalSpan.GetTag(string key) => GetTag(key);
    }
}
