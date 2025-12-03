// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <mutex>
#include <unordered_set>
#include <cstdint>

#include "INativeThreadList.h"
#include "ServiceBase.h"

class NativeThreadList
    : public ServiceBase,
      public INativeThreadList
{
public:
    NativeThreadList();
    ~NativeThreadList() = default;

private:
    NativeThreadList(const NativeThreadList&) = delete;
    NativeThreadList& operator=(const NativeThreadList&) = delete;

public:
    // Inherited via IService
    const char* GetName() override;

    // Inherited via INativeThreadList
    bool RegisterThread(uint32_t tid) override;
    bool Contains(uint32_t tid) const override;

private:
    const char* _serviceName = "NativeThreadList";

private:
    bool StartImpl() override;
    bool StopImpl() override;

    mutable std::mutex _mutex;
    std::unordered_set<uint32_t> _nativeThreadIds;
};