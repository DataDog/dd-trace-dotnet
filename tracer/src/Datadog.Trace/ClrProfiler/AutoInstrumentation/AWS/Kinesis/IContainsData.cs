// <copyright file="IContainsData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// Data interface for duck typing.
    /// </summary>
    internal interface IContainsData
    {
        /// <summary>
        /// Gets or sets the Kinesis Data.
        /// </summary>
        MemoryStream? Data { get; set;  }
    }
}
