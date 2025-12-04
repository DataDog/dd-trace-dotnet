#include "HybridUnwinder.h"

#include <libunwind.h>

#include "LibrariesInfoCache.h"
#include "JitCodeCache.h"
#include "UnwinderTracer.h"

// forward declaration
void RecordHybridEvent(UnwinderTracer* tracer, UnwinderTracer::Event event, uintptr_t value = 0, uintptr_t aux = 0, std::int32_t result = 0);

HybridUnwinder::HybridUnwinder(JitCodeCache* jitCodeCache) :
    _pJitCodeCache{jitCodeCache}
{
}

HybridUnwinder::~HybridUnwinder() = default;


// This Unwind function is only for ARM64 architecture.
std::int32_t HybridUnwinder::Unwind(void* ctx, uintptr_t* buffer, size_t bufferSize, UnwinderTracer* tracer)
{
    // This is a simplified version that doesn't require a LinuxStackFramesCollector instance
    // It mimics the hybrid unwinding logic but writes directly to the provided buffer
    
    if (bufferSize == 0) [[unlikely]]{
        return 0;
    }

    std::int32_t resultErrorCode;
    auto flag = UNW_INIT_SIGNAL_FRAME;
    unw_context_t context;
    
    if (ctx != nullptr)
    {
        context = *reinterpret_cast<unw_context_t*>(ctx);
    }
    else
    {
        resultErrorCode = unw_getcontext(&context);
        if (resultErrorCode != 0)
        {
            RecordHybridEvent(tracer, UnwinderTracer::Event::GetContextFailed, 0, 0, resultErrorCode);
            return 0; // Failed to get context
        }
        flag = static_cast<unw_init_local2_flags_t>(0);
    }
    
    if (tracer != nullptr)
    {
        tracer->SetInitFlags(static_cast<std::uint32_t>(flag));
    }

    unw_cursor_t cursor;
    resultErrorCode = unw_init_local2(&cursor, &context, flag);

    if (resultErrorCode < 0)
    {
        RecordHybridEvent(tracer, UnwinderTracer::Event::InitFailed, 0, 0, resultErrorCode);
        return 0;
    }
    
    size_t frameCount = 0;
    
    // Get only managed frames
    bool isManaged = false;
    // skip native frames
    unw_word_t ip = 0;
    do {
        if (unw_get_reg(&cursor, UNW_REG_IP, &ip) < 0)
        {
            RecordHybridEvent(tracer, UnwinderTracer::Event::GetIpFailed, 0, 0, resultErrorCode);
            return 0;
        }

        if (_pJitCodeCache->IsManagedCode(ip))
        {
            RecordHybridEvent(tracer, UnwinderTracer::Event::ManagedFrame, static_cast<uintptr_t>(ip));
            isManaged = true;
            break;
        }
        RecordHybridEvent(tracer, UnwinderTracer::Event::NativeFrame, static_cast<uintptr_t>(ip));
        resultErrorCode = unw_step(&cursor);
        RecordHybridEvent(tracer, UnwinderTracer::Event::StepResult, static_cast<uintptr_t>(ip), 0, resultErrorCode);
    } while (resultErrorCode > 0);
    
    
    if (!isManaged || resultErrorCode == 0) {
        RecordHybridEvent(tracer, UnwinderTracer::Event::Finish, 0, 0, 0);
        return 0;
    }

    buffer[frameCount++] = static_cast<uintptr_t>(ip);
    
    // Try manual unwinding for managed frames
    #if defined(__aarch64__)
    unw_word_t fp, lr;
    int fp_result = unw_get_reg(&cursor, UNW_AARCH64_X29, &fp);

    if (fp_result != 0) {
        RecordHybridEvent(tracer, UnwinderTracer::Event::ManualFramePointerReadFailed, 0, 0, fp_result);
        return 0;
    }
    
    if (fp_result == 0 && fp != 0)
    {
        // Walk the FP chain manually for managed frames
        uintptr_t current_fp = static_cast<uintptr_t>(fp);
        uintptr_t current_sp = static_cast<uintptr_t>(sp);
        
        while (frameCount < bufferSize) {
            const auto* methodInfo = _pJitCodeCache->FindMethod(static_cast<uintptr_t>(ip));
            if (methodInfo == nullptr)
            {
                RecordHybridEvent(tracer, UnwinderTracer::Event::ManagedDetectionMiss, static_cast<uintptr_t>(ip));
                break;
            }
            
            if (methodInfo->Start == -1 && methodInfo->End == -1)
            {
                RecordHybridEvent(tracer, UnwinderTracer::Event::ManagedViaProcMaps, ip);
            }
            else 
            {
                RecordHybridEvent(tracer, UnwinderTracer::Event::ManagedViaJitCache, ip, methodInfo->Start);
            }

            const int32_t cachedFpOffset =
                (methodInfo != nullptr && methodInfo->SavedFpOffset >= 0) ?
                    methodInfo->SavedFpOffset :
                    0;
            const int32_t cachedLrOffset =
                (methodInfo != nullptr && methodInfo->SavedLrOffset >= 0) ?
                    methodInfo->SavedLrOffset :
                    static_cast<int32_t>(sizeof(uintptr_t));
            
            unw_word_t prev_fp = 0;
            unw_word_t return_addr = 0;
            
            // Read stack memory safely using memcpy (signal-safe)
            std::memcpy(&prev_fp, reinterpret_cast<void*>(current_fp + cachedFpOffset), sizeof(prev_fp));
            std::memcpy(&return_addr, reinterpret_cast<void*>(current_fp + cachedLrOffset), sizeof(return_addr));
            
            // forward_sp check
            if (prev_fp < current_fp) {
                RecordHybridEvent(tracer, UnwinderTracer::Event::ManualFramePointerInvalidReturn, return_addr, current_fp | 0x1);
                break;
            }

            if (!IsValidReturnAddress(return_addr)) {
                RecordHybridEvent(tracer, UnwinderTracer::Event::ManualFramePointerInvalidReturn, return_addr, current_fp | 0x2);
                break;
            }
            
            RecordHybridEvent(tracer, UnwinderTracer::Event::ManualFramePointerSuccess, return_addr, current_fp);
        
            buffer[frameCount++] = static_cast<uintptr_t>(return_addr);
            current_fp = static_cast<uintptr_t>(prev_fp);
            ip = return_addr;
        }
    }
    #endif
    
    RecordHybridEvent(tracer, UnwinderTracer::Event::Finish, 0, 0, frameCount);
    return static_cast<std::int32_t>(frameCount);
}


bool HybridUnwinder::IsValidReturnAddress(uintptr_t address)
{
    // Check if address looks like a valid code address
    if (address == 0)
    {
        return false;
    }

    // Check reasonable alignment (ARM64 instructions are 4-byte aligned, x86_64 can be 1-byte)
#ifdef ARM64
    if ((address & 0x3) != 0)
    {
        return false;
    }
#endif

    // Basic sanity checks - address should be in a reasonable range
    // Typically code is above 0x10000 and below kernel space
    // ARM64 user space can go up to 0x0000ffffffffffff (48-bit addressing)
    // x86_64 user space typically up to 0x00007fffffffffff (47-bit addressing)
    #ifdef ARM64
    constexpr uintptr_t maxUserSpace = 0x0000ffffffffffffULL;
    #else
    constexpr uintptr_t maxUserSpace = 0x00007fffffffffffULL;
    #endif
    
    if (address < 0x10000 || address >= maxUserSpace)
    {
        return false;
    }

    // Check JIT cache first - most specific
    const auto* methodInfo = _pJitCodeCache->FindMethod(address);
    if (methodInfo != nullptr)
    {
        return true;
    }

    // Fallback heuristic for native return addresses
    return address >= 0x10000 && address < 0x7f0000000000ULL;
}


void RecordHybridEvent(UnwinderTracer* tracer, UnwinderTracer::Event event, uintptr_t value, uintptr_t aux, std::int32_t result)
{
    if (tracer == nullptr)
    {
        return;
    }
    tracer->Append(event, value, aux, result);
}