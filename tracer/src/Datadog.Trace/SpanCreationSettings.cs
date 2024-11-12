// <copyright file="SpanCreationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Settings to use when creating a new <see cref="IScope"/> and <see cref="ISpan"/>.
    /// </summary>
    public struct SpanCreationSettings
    {
        /// <summary>
        /// Gets or sets an explicit start time for the new span. If not set, uses the current time.
        /// </summary>
        public DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the new span's parent. To prevent a new span from inheriting a parent,
        /// set to <see cref="SpanContext.None"/>. If not set, defaults to <c>null</c> and
        /// the currently active span (if any) is used as the parent.
        /// </summary>
        public ISpanContext? Parent { get; set; }

        /// <summary>
        /// Gets or sets whether closing the new scope will close the contained span.
        /// If not set, defaults to <c>true</c>.
        /// </summary>
        public bool? FinishOnClose { get; set; }
    }
}
