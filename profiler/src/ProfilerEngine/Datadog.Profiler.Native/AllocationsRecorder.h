// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <memory>
#include <mutex>
#include <string_view>
#include <tuple>
#include <vector>

#include "IAllocationsRecorder.h"
#include "ServiceBase.h"

class IFrameStore;

class AllocationsRecorder :
    public IAllocationsRecorder,
    public ServiceBase
{
private:
    struct AllocInfo
    {
    public:
        AllocInfo(std::string_view name, uint32_t size);

    public:
        std::string_view Name;
        uint32_t Size;
    };

public:
    AllocationsRecorder(ICorProfilerInfo5* pCorProfilerInfo, IFrameStore* pFrameStore);

public:
    virtual const char* GetName() override;
    virtual void OnObjectAllocated(ObjectID objectId, ClassID classId) override;
    virtual bool Serialize(const std::string& filename) override;

private:
    bool StartImpl() override;
    bool StopImpl() override;

    ICorProfilerInfo5* _pCorProfilerInfo;
    IFrameStore* _pFrameStore;

private:
    std::mutex _lock;
    std::unique_ptr<std::vector<AllocInfo>> _allocations;
    std::atomic<uint64_t> _missed;
};
