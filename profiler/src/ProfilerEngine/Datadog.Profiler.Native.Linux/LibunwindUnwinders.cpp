// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibunwindUnwinders.h"

#include "IConfiguration.h"

#include <libunwind.h>
#include <ucontext.h>

std::unique_ptr<IUnwinder> LibunwindUnwinders::Create(IConfiguration const* const configuration)
{
    if (configuration->UseBacktrace2())
    {
        return std::make_unique<LibunwindUnwinders::UwnBacktrace2>();
    }

    return std::make_unique<LibunwindUnwinders::ManualUnwinder>();
}

std::size_t LibunwindUnwinders::ManualUnwinder::Unwind(void* ctx, shared::span<std::uintptr_t> frames)
{
    // if we are in the signal handler, ctx won't be null, so we will use the context
    // This will allow us to skip the syscall frame and start from the frame before the syscall.
    auto flag = UNW_INIT_SIGNAL_FRAME;

    std::int32_t resultErrorCode;

    unw_context_t context;
    if (ctx != nullptr)
    {
        context = *reinterpret_cast<unw_context_t*>(ctx);
    }
    else
    {
        // not in signal handler. Get the context and initialize the cursor form here
        resultErrorCode = unw_getcontext(&context);
        if (resultErrorCode != 0)
        {
            return 0;
        }

        flag = static_cast<unw_init_local2_flags_t>(0);
    }

    unw_cursor_t cursor;
    resultErrorCode = unw_init_local2(&cursor, &context, flag);

    if (resultErrorCode < 0)
    {
        return 0;
    }

    std::size_t nbFrames = 0;
    do
    {
        // should we handle stop inquiry from stacksamplermanagerloop
        unw_word_t ip;
        resultErrorCode = unw_get_reg(&cursor, UNW_REG_IP, &ip);
        if (resultErrorCode != 0)
        {
            return nbFrames;
        }

        frames[nbFrames] = ip;

        resultErrorCode = unw_step(&cursor);
    } while (resultErrorCode > 0 && nbFrames < frames.size());

    return nbFrames;
}

std::size_t LibunwindUnwinders::UwnBacktrace2::Unwind(void* ctx, shared::span<std::uintptr_t> frames)
{
    auto* context = reinterpret_cast<unw_context_t*>(ctx);

    auto count = unw_backtrace2((void**)frames.data(), frames.size(), context);

    return count;
}
