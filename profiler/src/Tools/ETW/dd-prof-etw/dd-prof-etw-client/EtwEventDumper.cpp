// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EtwEventDumper.h"

#include "..\..\..\..\ProfilerEngine\Datadog.Profiler.Native\ClrEventsParser.h"


#include <sstream>
#include <iomanip>
#include <iostream>

bool EtwEventDumper::BuildClrEvent(
    std::string& name,
    uint32_t tid, uint8_t version, uint16_t id, uint64_t keyword, uint8_t level,
    uint32_t cbEventData, const uint8_t* pEventData)
{
    std::stringstream buffer;
    if (keyword == KEYWORD_GC)
    {
        switch (id)
        {
            case EVENT_ALLOCATION_TICK:
                buffer << "AllocationTick";
                return false;

            case EVENT_GC_TRIGGERED:
                buffer << "GCTriggered";
                return false;

            case EVENT_GC_START:
            {
                buffer << "GCStart";
                if (cbEventData >= sizeof(GCStartPayload))
                {
                    GCStartPayload* pPayload = (GCStartPayload*)pEventData;
                    buffer << " #" << pPayload->Count << " gen" << pPayload->Depth;
                }
            }
            break;

            case EVENT_GC_END:
            {
                buffer << "GCEnd";
                if (cbEventData >= sizeof(GCEndPayload))
                {
                    GCEndPayload* pPayload = (GCEndPayload*)pEventData;
                    buffer << " #" << pPayload->Count << " gen" << pPayload->Depth;
                }
            }
            break;

            case EVENT_GC_HEAP_STAT:
                buffer << "GCHeapStat";
                return false;

            case EVENT_GC_GLOBAL_HEAP_HISTORY:
            {
                buffer << "GCGlobalHeapHistory";
                if (cbEventData >= sizeof(GCGlobalHeapPayload))
                {
                    GCGlobalHeapPayload* pPayload = (GCGlobalHeapPayload*)pEventData;
                    buffer << " gen" << pPayload->CondemnedGeneration;
                }
            }
            break;

            case EVENT_GC_SUSPEND_EE_BEGIN:
                buffer << "GCSuspendEEBegin";
                return false;

            case EVENT_GC_RESTART_EE_END:
                buffer << "GCRestartEEEnd";
                return false;

            case EVENT_GC_PER_HEAP_HISTORY:
                buffer << "GCPerHeapHistory";
                return false;

            case EVENT_GC_JOIN:
                buffer << "GCJOIN";
                return false;

            case EVENT_GC_MARKWITHTYPE:
                buffer << "GCMARKWITHTYPE";
                return false;

            case EVENT_GC_PINOBJECTATGCTIME:
                buffer << "GCPINOBJECTATGCTIME";
                return false;

            default:
            {
                buffer << "GC-" << id;
            }
            return false;
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
        return false;
    }
    else
    {
        buffer << "?-" << id;
    }

    buffer << " v" << (uint16_t)version;
    name = buffer.str();

    return true;
}

void EtwEventDumper::DumpCallstack(uint32_t cbEventData, const uint8_t* pEventData)
{
    if (cbEventData == 0)
    {
        return;
    }

    if (cbEventData < sizeof(StackWalkPayload))
    {
        return;
    }

    StackWalkPayload* pPayload = (StackWalkPayload*)pEventData;

    //                 size of all frames                      + payload size             - size of the first frame not counted twice
    if (cbEventData < pPayload->FrameCount * sizeof(uintptr_t) + sizeof(StackWalkPayload) - sizeof(uintptr_t))
    {
        //std::cout << "   Invalid payload size: " << cbEventData << " bytes for " << pPayload->FrameCount << " frames\n";
        return;
    }

    for (uint32_t i = 0; i < pPayload->FrameCount; ++i)
    {
        std::cout << "   0x" << std::setw(16) << std::setfill('0') << std::hex << pPayload->Stack[i] << std::dec << "\n";
    }
}

void EtwEventDumper::DumpAllocationTick(uint32_t cbEventData, const uint8_t* pEventData)
{
    if (cbEventData == 0)
    {
        return;
    }

    if (cbEventData < sizeof(AllocationTickV3Payload))
    {
        return;
    }

    AllocationTickV3Payload* pPayload = (AllocationTickV3Payload*)pEventData;
    if (pPayload->AllocationKind == 0)
    {
        std::wcout << L"   small | ";
    }
    else
    {
        std::wcout << L"   large | ";
    }
    std::wcout << (wchar_t*)&(pPayload->FirstCharInName) << L"\n";
}

void EtwEventDumper::OnEvent(
    etw_timestamp timestamp,
    uint32_t tid,
    uint32_t version,
    uint64_t keyword,
    uint8_t level,
    uint32_t id,
    uint32_t cbEventData,
    const uint8_t* pEventData)
{
    std::string name;
    if (BuildClrEvent(name, tid, version, id, keyword, level, cbEventData, pEventData))
    {
        std::cout
            //<< std::setw(16) << std::setfill('0') << timestamp
            << " (0x" << std::setw(8) << std::setfill(' ') << std::hex << keyword << std::dec << ", " << (uint16_t)level << ")"
            << " " << std::setw(4) << std::setfill(' ') << id << " | "
            << std::setw(6) << std::setfill(' ') << tid << " | " << name
            << "\n";

        if (keyword == KEYWORD_STACKWALK)
        {
            if (id == EVENT_SW_STACK)
            {
                //DumpCallstack(cbEventData, pEventData);
            }
        }
        else
        if (keyword == KEYWORD_GC)
        {
            if (id == EVENT_ALLOCATION_TICK)
            {
                DumpAllocationTick(cbEventData, pEventData);
            }
        }
    }
    else
    {
        //std::cout << "   Impossible to get CLR event details...\n";
    }
}

void EtwEventDumper::OnStop()
{
}
