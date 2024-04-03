// <copyright file="NonBatchUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Sink;

internal class NonBatchUploader : IBatchUploader
{
    public Task Upload(IEnumerable<string> payloads)
    {
        return Task.CompletedTask;
    }
}
