// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <unordered_map>

#include <fstream>
//#include <iostream>

#include "AllocationsRecorder.h"
#include "IFrameStore.h"

AllocationsRecorder::AllocInfo::AllocInfo(std::string_view name, uint32_t size)
{
    Name = name;
    Size = size;
}


AllocationsRecorder::AllocationsRecorder(ICorProfilerInfo5* pCorProfilerInfo, IFrameStore* pFrameStore)
    :
    _pCorProfilerInfo {pCorProfilerInfo},
    _pFrameStore {pFrameStore},
    _missed{0}
{
    // 1 million allocations
    _allocations = std::make_unique<std::vector<AllocInfo>>();
    _allocations->reserve(1000000);
}

void AllocationsRecorder::OnObjectAllocated(ObjectID objectId, ClassID classId)
{
    // TODO: use FrameStore::GetTypeName(classId, string_view) to get the name of the corresponding type (no namespace)
    //           ICorProfilerInfo::GetObjectSize(objectId)

    std::string_view typeName;
    if (!_pFrameStore->GetTypeName(classId, typeName))
    {
        _missed.fetch_add(1);

        // the doc states that it is possible to be notified BEFORE the class has even be loaded by the CLR
        // https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilercallback-objectallocated-method
        return;
    }

    ULONG size = 0;
    auto hr = _pCorProfilerInfo->GetObjectSize(objectId, &size);
    if (FAILED(hr))
    {
        return;
    }

    {
        std::lock_guard<std::mutex> lock(_lock);

        AllocInfo info(typeName, size);
        _allocations->push_back(info);
    }
}

bool AllocationsRecorder::Serialize(const std::string& filename)
{
    std::unique_ptr<std::vector<AllocInfo>> pAllocations;

    {
        std::lock_guard<std::mutex> lock(_lock);

        pAllocations = std::move(_allocations);
        _allocations = std::make_unique<std::vector<AllocInfo>>();
        _allocations->reserve(1000000);
    }

    std::ofstream file{filename, std::ios::out | std::ios::binary};

    // save string table
    // each string_view is used as a key to a unique string because they are all pointing to strings in the FrameStore cache
    std::unordered_map<std::string_view, uint32_t> stringTable;
    uint32_t current = 0;
    char endOfString = '\0';
    for (auto& alloc : *pAllocations.get())
    {
        auto entry = stringTable.find(alloc.Name);
        if (entry == stringTable.end())
        {
            stringTable[alloc.Name] = current++;
            file.write(alloc.Name.data(), alloc.Name.size());
            file.write(&endOfString, 1);
        }
    }

    // the last string is an empty string
    file.write(&endOfString, 1);

    // save allocations
    uint32_t stringId;
    uint32_t size;
    for (auto& alloc : *pAllocations.get())
    {
        auto entry = stringTable.find(alloc.Name);
        stringId = entry->second;
        size = alloc.Size;

        // the size of an int32 is 4 bytes; i.e. 4 characters
        file.write((const char*)&stringId, 4);
        file.write((const char*)&size, 4);
    }

    file.close();

    return !file.fail();
}

const char* AllocationsRecorder::GetName()
{
    return "AllocationsRecorder";
}

bool AllocationsRecorder::StartImpl()
{
    return true;
}

bool AllocationsRecorder::StopImpl()
{
    return true;
}