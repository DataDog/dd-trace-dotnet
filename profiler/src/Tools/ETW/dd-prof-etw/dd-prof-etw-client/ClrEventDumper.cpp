// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ClrEventDumper.h"

#include <sstream>
#include <iomanip>
#include <iostream>


// keywords
const int KEYWORD_CONTENTION = 0x00004000;
const int KEYWORD_GC = 0x00000001;
const int KEYWORD_STACKWALK = 0x40000000;

// events id
const int EVENT_CONTENTION_STOP = 91; // version 1 contains the duration in nanoseconds
const int EVENT_CONTENTION_START = 81;

const int EVENT_ALLOCATION_TICK = 10; // version 4 contains the size + reference
const int EVENT_GC_TRIGGERED = 35;
const int EVENT_GC_START = 1;                 // V2
const int EVENT_GC_END = 2;                   // V1
const int EVENT_GC_HEAP_STAT = 4;             // V1
const int EVENT_GC_GLOBAL_HEAP_HISTORY = 205; // V2
const int EVENT_GC_SUSPEND_EE_BEGIN = 9;      // V1
const int EVENT_GC_RESTART_EE_END = 3;        // V2

const int EVENT_GC_JOIN = 203;
const int EVENT_GC_PER_HEAP_HISTORY = 204;
const int EVENT_GC_MARKWITHTYPE = 202;
const int EVENT_GC_PINOBJECTATGCTIME = 33;

const int EVENT_SW_STACK = 82;


bool ClrEventDumper::BuildClrEvent(
    std::string& name,
    uint32_t tid, uint8_t version, uint16_t id, uint64_t keyword, uint8_t level)
{
    std::stringstream buffer;
    if (keyword == KEYWORD_GC)
    {
        switch (id)
        {
            case EVENT_ALLOCATION_TICK:
                buffer << "AllocationTick";
                break;

            case EVENT_GC_TRIGGERED:
                buffer << "GCTriggered";
                break;

            case EVENT_GC_START:
                buffer << "GCStart";
                break;

            case EVENT_GC_END:
                buffer << "GCEnd";
                break;

            case EVENT_GC_HEAP_STAT:
                buffer << "GCHeapStat";
                break;

            case EVENT_GC_GLOBAL_HEAP_HISTORY:
                buffer << "GCGlobalHeapHistory";
                break;

            case EVENT_GC_SUSPEND_EE_BEGIN:
                buffer << "GCSuspendEEBegin";
                break;

            case EVENT_GC_RESTART_EE_END:
                buffer << "GCRestartEEEnd";
                break;

            case EVENT_GC_PER_HEAP_HISTORY:
                buffer << "GCPerHeapHistory";
                break;

            case EVENT_GC_JOIN:
                buffer << "GCJOIN";
                break;

            case EVENT_GC_MARKWITHTYPE:
                buffer << "GCMARKWITHTYPE";
                break;

            case EVENT_GC_PINOBJECTATGCTIME:
                buffer << "GCPINOBJECTATGCTIME";
                break;

            default:
            {
                buffer << "GC-" << id;
            }
            break;
        }
    }
    else if (keyword == KEYWORD_CONTENTION)
    {
        if (id == EVENT_CONTENTION_STOP)
        {
            buffer << "ContentionStop";
        }
        else if (id == EVENT_CONTENTION_START)
        {
            buffer << "ContentionStart";
        }
        else
        {
            buffer << "Lock-" << id;
        }
    }
    else if (keyword == KEYWORD_STACKWALK)
    {
        if (id == EVENT_SW_STACK)
        {
            buffer << "StackWalk";
        }
        else
        {
            buffer << "SW-" << id;
        }
    }
    else
    {
        buffer << "?-" << id;
    }

    buffer << " v" << (uint16_t)version;
    name = buffer.str();

    return true;
}

void ClrEventDumper::OnEvent(
    uint32_t tid,
    uint32_t version,
    uint64_t keyword,
    uint8_t level,
    uint32_t id,
    uint32_t cbEventData,
    const uint8_t* pEventData)
{
    std::string name;
    if (BuildClrEvent(name, tid, version, id, keyword, level))
    {
        std::cout
            << " (0x" << std::setw(8) << std::setfill(' ') << std::hex << keyword << std::dec << ", " << (uint16_t)level << ")"
            << " " << std::setw(4) << std::setfill(' ') << id << " | "
            << std::setw(6) << std::setfill(' ') << tid << " | " << name
            << "\n";
    }
    else
    {
        std::cout << "   Impossible to get CLR event details...\n";
    }
}

void ClrEventDumper::OnStop()
{
}
