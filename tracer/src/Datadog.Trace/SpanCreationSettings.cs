﻿// <copyright file="SpanCreationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Settings to use when creating a new <see cref="IScope"/> and <see cref="ISpan"/>
    /// </summary>
    public struct SpanCreationSettings
    {
        /// <summary>
        /// Gets or sets an explicit start time for the span. If not set, uses the current time.
        /// </summary>
        public DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the span's parent. If not set, the current active span context will be used,
        /// or a new one will be created if none is currently active.
        /// </summary>
        public ISpanContext Parent { get; set; }

        /// <summary>
        /// Gets or sets whether closing the scope will close the contained span.
        /// If not set, defaults to <c>true</c>.
        /// </summary>
        public bool? FinishOnClose { get; set; }
    }
}
