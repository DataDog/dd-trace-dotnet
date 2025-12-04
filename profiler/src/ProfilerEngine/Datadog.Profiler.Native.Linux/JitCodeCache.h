#pragma once

#include "cor.h" // IWYU pragma: keep
#include "corprof.h"


#include "AutoResetEvent.h"
#include "IFrameStore.h"
#include "ServiceBase.h"

#include <array>
#include <atomic>
#include <cstdint>
#include <forward_list>
#include <thread>
#include <future>


inline constexpr mdFieldDef JitCodeCacheMetadataTokenDummy = 0;

class JitCodeCache : public ServiceBase
{
public:
    struct MethodInfo
    {
        static constexpr size_t MaxSavedRegisters = 16;
        static constexpr size_t MaxRecordedBytes = 64;

        uintptr_t Start = 0;
        uintptr_t End = 0;
        FunctionID Function = 0;
        ModuleID Module = 0;
        ClassID Class = 0;
        ULONG32 PrologSize = 0;
        ULONG32 RangeCount = 0;
        uint32_t FrameSize = 0;
        int32_t SavedFpOffset = -1;
        int32_t SavedLrOffset = -1;
        uint32_t CalleeSavedRegisterMask = 0;
        uint8_t SavedRegisterCount = 0;
        // only for debugging purposes ??
        std::array<uint8_t, MaxSavedRegisters> SavedRegisters{};
        std::array<int16_t, MaxSavedRegisters> SavedRegisterOffsets{};
        uint16_t PrologLength = 0;
        std::array<uint8_t, MaxRecordedBytes> PrologBytes{};
    };

    // Keep frame store for the moment, we'll decide if we need it later or not.
    JitCodeCache(ICorProfilerInfo5* profilerInfo, IFrameStore* frameStore);

    ~JitCodeCache();

    JitCodeCache(const JitCodeCache&) = delete;
    JitCodeCache& operator=(const JitCodeCache&) = delete;
    JitCodeCache(JitCodeCache&&) = delete;
    JitCodeCache& operator=(JitCodeCache&&) = delete;

    bool StartImpl() override;
    bool StopImpl() override;

    const char* GetName() override;

    const MethodInfo* FindMethod(uintptr_t address) const;
    void RegisterMethod(FunctionID functionId);
    bool IsManagedCode(uintptr_t ip) const;

private:
    struct Node
    {
        MethodInfo Info{};
        Node* Next = nullptr;
    };

    std::atomic<Node*> _head;

    void Work();

    void RegisterMethodImpl(FunctionID functionId);
    bool TryGetFunctionIdentity(FunctionID functionId, ClassID& classId, ModuleID& moduleId, mdToken& methodToken);
    bool TryComputeCodeLayout(FunctionID functionId, uintptr_t& minStart, uintptr_t& maxEnd, size_t& totalSize, ULONG32& rangeCount);
    ULONG32 ComputePrologSize(FunctionID functionId, ULONG32& ilMapCountOut);
    void ResolveFrameDisplay(uintptr_t minStart, std::string& frameDisplay, std::string& moduleDisplay);
    void RegisterMethodImpl2(const MethodInfo& info);

    std::thread _worker;
    std::atomic_bool _stopRequested;
    AutoResetEvent _workerEvent;
    std::mutex _workerQueueMutex;
    std::forward_list<FunctionID> _workerQueue;
    ICorProfilerInfo5* _profilerInfo;
    IFrameStore* _frameStore;
};

