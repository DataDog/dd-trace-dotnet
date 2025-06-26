// <copyright file="IContainsRecords.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// Interface for types that contain a Records collection.
    /// </summary>
    internal interface IContainsRecords
    {
        /// <summary>
        /// Gets or sets the Records collection.
        /// </summary>
        IList? Records { get; set; }
    }
}
