// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "UnwinderTracer.h"

#include <iomanip>
#include <ostream>

static const char* EventTypeName(EventType t)
{
    switch (t)
    {
        case EventType::Start:             return "Start";
        case EventType::InitCursor:        return "InitCursor";
        case EventType::NativeFrame:       return "NativeFrame";
        case EventType::ManagedTransition: return "ManagedTransition";
        case EventType::LibunwindStep:     return "LibunwindStep";
        case EventType::FrameChainStep:    return "FrameChainStep";
        case EventType::Finish:            return "Finish";
        default:                           return "Unknown";
    }
}

static const char* FinishReasonName(FinishReason r)
{
    switch (r)
    {
        case FinishReason::Success:             return "Success";
        case FinishReason::BufferFull:          return "BufferFull";
        case FinishReason::FailedGetContext:     return "FailedGetContext";
        case FinishReason::FailedInitLocal2:    return "FailedInitLocal2";
        case FinishReason::FailedGetReg:        return "FailedGetReg";
        case FinishReason::FailedLibunwindStep: return "FailedLibunwindStep";
        case FinishReason::NoStackBounds:       return "NoStackBounds";
        case FinishReason::InvalidFp:           return "InvalidFp";
        case FinishReason::TooManyNativeFrames: return "TooManyNativeFrames";
        case FinishReason::InvalidIp:           return "InvalidIp";
        case FinishReason::FailedIsManaged:     return "FailedIsManaged";
        default:                                return "Unknown";
    }
}

void UnwinderTracer::WriteTo(std::ostream& os) const
{
    auto recorded = RecordedEvents();
    os << "# UnwinderTrace: " << recorded << " events recorded, "
       << _totalEvents << " total";
    if (Overflowed())
        os << " (" << (_totalEvents - Capacity) << " discarded)";
    os << "\n";

    for (std::size_t i = 0; i < recorded; ++i)
    {
        const auto& e = _entries[i];
        os << "[" << std::setw(3) << i << "] "
           << std::left << std::setw(20) << EventTypeName(e.eventType);

        switch (e.eventType)
        {
            case EventType::Start:
                os << " result=" << e.result;
                break;

            case EventType::Finish:
                os << " result=" << e.result
                   << " reason=" << FinishReasonName(e.finishReason);
                break;

            case EventType::InitCursor:
            case EventType::LibunwindStep:
            {
                const auto& cs = e.cursorSnapshot;
                os << " result=" << e.result
                   << "  cursor={ ip=0x" << std::hex << cs.ip
                   << " cfa=0x" << cs.cfa
                   << " locFp=0x" << cs.locFp
                   << " locLr=0x" << cs.locLr
                   << " locSp=0x" << cs.locSp
                   << std::dec
                   << " nextToSignalFrame=" << cs.nextToSignalFrame
                   << " cfaIsUnreliable=" << cs.cfaIsUnreliable
                   << " frameType=" << cs.frameType
                   << " cfaRegSp=" << cs.cfaRegSp
                   << " cfaRegOffset=" << cs.cfaRegOffset
                   << " }";
                break;
            }

            case EventType::NativeFrame:
                os << " ip=0x" << std::hex << e.ip
                   << " fp=0x" << e.fp
                   << " sp=0x" << e.sp << std::dec;
                break;

            case EventType::ManagedTransition:
            case EventType::FrameChainStep:
                os << " ip=0x" << std::hex << e.ip
                   << " fp=0x" << e.fp << std::dec;
                break;
        }
        os << "\n";
    }
}
