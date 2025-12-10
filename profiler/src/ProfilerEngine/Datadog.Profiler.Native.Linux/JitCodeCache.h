#pragma once

#include "cor.h" // IWYU pragma: keep
#include "corprof.h"

#include <array>
#include <atomic>
#include <cstdint>

inline constexpr mdFieldDef JitCodeCacheMetadataTokenDummy = 0;

class JitCodeCache
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
        std::array<uint8_t, MaxSavedRegisters> SavedRegisters{};
        std::array<int16_t, MaxSavedRegisters> SavedRegisterOffsets{};
        uint16_t PrologLength = 0;
        std::array<uint8_t, MaxRecordedBytes> PrologBytes{};
    };

    static JitCodeCache& Instance();

    ~JitCodeCache();

    void RegisterMethod(const MethodInfo& info);
    const MethodInfo* FindMethod(uintptr_t address) const;

private:
    struct Node
    {
        MethodInfo Info{};
        Node* Next = nullptr;
    };

    std::atomic<Node*> _head;

    JitCodeCache();

    JitCodeCache(const JitCodeCache&) = delete;
    JitCodeCache& operator=(const JitCodeCache&) = delete;
    JitCodeCache(JitCodeCache&&) = delete;
    JitCodeCache& operator=(JitCodeCache&&) = delete;
};

