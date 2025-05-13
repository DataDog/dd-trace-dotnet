// <copyright file="IEventDumper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests
{
    public interface IEventDumper
    {
        public abstract void DumpEvent(
            UInt64 timestamp,
            UInt32 tid,
            UInt32 version,
            UInt64 keyword,
            byte level,
            UInt32 id,
            Span<byte> eventData);
    }
}
