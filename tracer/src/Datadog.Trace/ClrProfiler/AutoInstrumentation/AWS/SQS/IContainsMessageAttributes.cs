// <copyright file="IContainsMessageAttributes.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections;
using System.IO;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// MessageAttributes interface for ducktyping
    /// </summary>
    internal interface IContainsMessageAttributes
    {
        /// <summary>
        /// Gets or sets the message attributes
        /// </summary>
        IDictionary? MessageAttributes { get; set; } // <string, IMessageAttributeValue>
    }

    internal interface IMessageAttributeValue
    {
        string? DataType { get; set; } // can be String, Number, or Binary

        string? StringValue { get; set; } // filled if DataType is String or Number

        MemoryStream? BinaryValue { get; set; } // filled if DataType is Binary
    }
}
