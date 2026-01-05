// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Backtrace2Unwinder.h"

#include <libunwind.h>

Backtrace2Unwinder::Backtrace2Unwinder() = default;

std::int32_t Backtrace2Unwinder::Unwind(void* ctx, std::uintptr_t* buffer, std::size_t bufferSize) const
{
    // unw_backtrace2 handles the case ctx == nullptr
    auto* context = reinterpret_cast<unw_context_t*>(ctx);

    return unw_backtrace2(reinterpret_cast<void**>(buffer), bufferSize, context, UNW_INIT_SIGNAL_FRAME);
}