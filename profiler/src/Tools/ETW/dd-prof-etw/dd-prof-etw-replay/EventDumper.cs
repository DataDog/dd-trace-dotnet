// <copyright file="EventDumper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests
{
    public class EventDumper : IEventDumper
    {
        public void DumpEvent(ulong timestamp, uint tid, uint version, ulong keyword, byte level, uint id, Span<byte> pEventData)
        {
            Console.WriteLine($"{timestamp,12} | {tid,6} - [{keyword,8:x}, {level,2}] = ({id,3}, {version,2}) with {pEventData.Length} bytes");
        }
    }
}
