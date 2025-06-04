// <copyright file="EventDumper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Profiler.IntegrationTests
{
    public class EventDumper : IEventDumper
    {
        // keywords
        private const ulong KeywordContention = 0x00004000;
        private const ulong KeywordStack = 0x40000000;
        private const ulong KeywordGC = 0x00000001;

        // events
#pragma warning disable SA1310 // Field names should not contain underscore
        private const int EVENT_CONTENTION_STOP = 91; // version 1 contains the duration in nanoseconds
        private const int EVENT_CONTENTION_START = 81;

        private const int EVENT_SW_STACK = 82;

        private const int EVENT_ALLOCATION_TICK = 10; // version 4 contains the size + reference
        private const int EVENT_GC_TRIGGERED = 35;
        private const int EVENT_GC_START = 1;                 // V2
        private const int EVENT_GC_END = 2;                   // V1
        private const int EVENT_GC_HEAP_STAT = 4;             // V1
        private const int EVENT_GC_PER_HEAP_HISTORY = 204;
        private const int EVENT_GC_GLOBAL_HEAP_HISTORY = 205; // V2
        private const int EVENT_GC_SUSPEND_EE_BEGIN = 9;      // V1
        private const int EVENT_GC_RESTART_EE_END = 3;        // V2
#pragma warning restore SA1310 // Field names should not contain underscore

        private int _lastGC = 0;

        public void DumpEvent(ulong timestamp, uint tid, uint version, ulong keyword, byte level, uint id, Span<byte> eventData)
        {
            //Console.WriteLine($"{timestamp,12} | {tid,6} - [{keyword,8:x}, {level,2}] = ({id,3}, {version,2}) with {eventData.Length} bytes");
            var description = GetEventName(keyword, id, eventData);
            if (description != id.ToString())
            {
                Console.WriteLine($"                      {description}");
            }
        }

        private string GetEventName(ulong keyword, uint id, Span<byte> eventData)
        {
            switch (keyword)
            {
                case KeywordGC:
                    return GetGCEventName(id, eventData);

                case KeywordContention:
                    return GetContentionEvenName(id, eventData);

                case KeywordStack:
                    if (id == EVENT_SW_STACK)
                    {
                        return "Stack";
                    }
                    else
                    {
                        return id.ToString();
                    }

                default:
                    return id.ToString();
            }
        }

        private string GetGCEventName(uint id, Span<byte> eventData)
        {
            switch (id)
            {
                case EVENT_GC_TRIGGERED:
                {
                    return $"GC Triggered";
                }

                case EVENT_GC_START:
                {
                    MemoryMarshal.TryRead(eventData, out int count);
                    MemoryMarshal.TryRead(eventData.Slice(4), out int generation);
                    return $"GC Start #{count} gen {generation}";
                }

                case EVENT_GC_END:
                {
                    MemoryMarshal.TryRead(eventData, out int count);
                    MemoryMarshal.TryRead(eventData.Slice(4), out int generation);
                    return $"GC End #{count} gen {generation}";
                }

                case EVENT_GC_HEAP_STAT:
                    return "GC Heap Stat";
                case EVENT_GC_PER_HEAP_HISTORY:
                    return "GC Per Heap History";
                case EVENT_GC_GLOBAL_HEAP_HISTORY:
                    return "GC Global Heap History";
                case EVENT_GC_SUSPEND_EE_BEGIN:
                    return "GC Suspend EE Begin";
                case EVENT_GC_RESTART_EE_END:
                    return "GC Restart EE End";
                default:
                    return id.ToString();
            }
        }

        private string GetContentionEvenName(uint id, Span<byte> eventData)
        {
            switch (id)
            {
                case EVENT_CONTENTION_START:
                    return "Contention Start";
                case EVENT_CONTENTION_STOP:
                    return "Contention Stop";
                default:
                    return id.ToString();
            }
        }
    }
}
