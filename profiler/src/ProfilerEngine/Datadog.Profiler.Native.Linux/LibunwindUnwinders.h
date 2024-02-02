// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IUnwinder.h"

#include <memory>

class IConfiguration;

class LibunwindUnwinders
{
public:
    static std::unique_ptr<IUnwinder> Create(IConfiguration const* const configuration);

private:
    LibunwindUnwinders() = default;

    class ManualUnwinder : public IUnwinder
    {
    public:
        ManualUnwinder() = default;
        ~ManualUnwinder() override = default;

        std::size_t Unwind(void* ctx, shared::span<std::uintptr_t> frames) override;
    };

    class UwnBacktrace2 : public IUnwinder
    {
    public:
        UwnBacktrace2() = default;
        ~UwnBacktrace2() override = default;

        std::size_t Unwind(void* ctx, shared::span<std::uintptr_t> frames) override;
    };
};