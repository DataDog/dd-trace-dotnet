// <copyright file="IGetRecordsResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// GetRecordsRequest interface for duck typing.
    /// </summary>
    internal interface IGetRecordsResponse : IDuckType
    {
        IList Records { get; } // <IRecord>
    }

    internal interface IRecord
    {
        MemoryStream? Data { get; }
    }
}
