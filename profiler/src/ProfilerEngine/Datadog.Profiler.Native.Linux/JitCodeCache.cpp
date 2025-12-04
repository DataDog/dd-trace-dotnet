#include "JitCodeCache.h"

#include "LibrariesInfoCache.h"
#include "Log.h"

#include <chrono>
#include <memory>

using namespace std::chrono_literals;

void LogJitMetadata(
    FunctionID functionId,
    ModuleID moduleId,
    ClassID classId,
    uintptr_t minStart,
    uintptr_t maxEnd,
    size_t totalSize,
    ULONG32 rangeCount,
    ULONG32 ilMapCount,
    ULONG32 prologSize,
    BOOL safeToBlock,
    const std::string& frameDisplay,
    const std::string& moduleDisplay);

struct Arm64PrologueSummary
{
    uint32_t FrameSize = 0;
    int32_t SavedFpOffset = -1;
    int32_t SavedLrOffset = -1;
    uint32_t CalleeSavedMask = 0;
    uint8_t SavedRegisterCount = 0;
    std::array<uint8_t, JitCodeCache::MethodInfo::MaxSavedRegisters> Registers{};
    std::array<int16_t, JitCodeCache::MethodInfo::MaxSavedRegisters> Offsets{};
    uint16_t PrologLength = 0;
    std::array<uint8_t, JitCodeCache::MethodInfo::MaxRecordedBytes> PrologBytes{};
};

Arm64PrologueSummary AnalyzeArm64Prologue(uintptr_t start, size_t prologSize, uintptr_t end);

JitCodeCache::JitCodeCache(ICorProfilerInfo5* profilerInfo, IFrameStore* frameStore) :
    _head(nullptr),
    _profilerInfo(profilerInfo),
    _frameStore(frameStore),
    _stopRequested(false),
    _workerEvent(false),
    _workerQueueMutex(),
    _workerQueue()
{
}

JitCodeCache::~JitCodeCache()
{
    StopImpl();
}

bool JitCodeCache::StartImpl()
{
    std::promise<void> startEvent;
    std::future<void> startFuture = startEvent.get_future();
    _worker = std::thread([this, startEvent = std::move(startEvent)]() mutable
    {
        startEvent.set_value();
        Work();
    });

    return startFuture.wait_for(1s) == std::future_status::ready;
}

// TODO: we must make sure that JitCodeCache instance is the last one to be stopped.
bool JitCodeCache::StopImpl()
{
    _stopRequested = true;
    _workerEvent.Set();

    Node* current = _head.load(std::memory_order_acquire);
    while (current != nullptr)
    {
        std::unique_ptr<Node> holder(current);
        current = holder->Next;
    }

    _worker.join();
    return true;
}

const char* JitCodeCache::GetName()
{
    return "JitCodeCache";
}

void JitCodeCache::Work()
{
    while (true)
    {
        _workerEvent.Wait();

        if (_stopRequested)
        {
            break;
        }

        // in case of spurious wakeup
        if (_workerQueue.empty())
        {
            continue;
        }

        while (!_workerQueue.empty()) [[likely]]
        {
            FunctionID functionId = 0;
            {
                std::lock_guard<std::mutex> lock(_workerQueueMutex);
                functionId = _workerQueue.front();
                _workerQueue.pop_front();
            }
            RegisterMethodImpl(functionId);
        }
    }
}

void JitCodeCache::RegisterMethod(FunctionID functionId)
{
    if (_stopRequested)
    {
        return;
    }

    {
        std::lock_guard<std::mutex> lock(_workerQueueMutex);
        _workerQueue.push_front(functionId);
    }
    _workerEvent.Set();
}

void JitCodeCache::RegisterMethodImpl2(const MethodInfo& info)
{
    auto node = std::make_unique<Node>();
    node->Info = info;
    node->Next = nullptr;

    Node* rawNode = node.get();
    Node* current = _head.load(std::memory_order_acquire);
    while (true)
    {
        rawNode->Next = current;
        if (_head.compare_exchange_weak(current, rawNode, std::memory_order_release, std::memory_order_acquire))
        {
            node.release();
            break;
        }
    }
}

const JitCodeCache::MethodInfo* JitCodeCache::FindMethod(uintptr_t address) const
{
    Node* current = _head.load(std::memory_order_acquire);
    while (current != nullptr)
    {
        const auto& method = current->Info;
        if (address >= method.Start && address < method.End)
        {
            return &method;
        }

        current = current->Next;
    }

    // check procmaps for now, should be removed once we know why the jit code cache
    // does not have the function info
    auto* librariesCache = LibrariesInfoCache::GetInstance();
    if (librariesCache != nullptr)
    {
        static MethodInfo defaultMethodInfo;
        static std::once_flag onceFlag;

        std::call_once(onceFlag, []() {
            defaultMethodInfo = MethodInfo{
                .Start = -1, 
                .End = -1,
                .Function = -1,
                .Module = -1,
                .Class = -1,
                .PrologSize = -1,
                .RangeCount = -1,
                .FrameSize = -1,
                .SavedFpOffset = 0,
                .SavedLrOffset = static_cast<int32_t>(sizeof(uintptr_t)),
                .CalleeSavedRegisterMask = -1,
                .SavedRegisterCount = -1,
                .PrologLength = -1,
                .PrologBytes = {},
            };
        });

        if (librariesCache->IsAddressInManagedRegion(static_cast<uintptr_t>(address)))
        {
            return &defaultMethodInfo;
        }
    }
    return nullptr;
}

void JitCodeCache::RegisterMethodImpl(FunctionID functionId)
{
    ClassID classId = 0;
    ModuleID moduleId = 0;
    mdToken methodToken = 0;

    if (!TryGetFunctionIdentity(functionId, classId, moduleId, methodToken))
    {
        return;
    }

    uintptr_t minStart = 0;
    uintptr_t maxEnd = 0;
    size_t totalSize = 0;
    ULONG32 rangeCount = 0;
    if (!TryComputeCodeLayout(functionId, minStart, maxEnd, totalSize, rangeCount))
    {
        return;
    }

    ULONG32 ilMapCount = 0;
    const ULONG32 prologSize = ComputePrologSize(functionId, ilMapCount);

    std::string frameDisplay;
    std::string moduleDisplay;
    ResolveFrameDisplay(minStart, frameDisplay, moduleDisplay);

    if (frameDisplay.empty())
    {
        frameDisplay = "<unknown>";
    }
    if (moduleDisplay.empty())
    {
        moduleDisplay = "<unknown>";
    }

#ifdef ARM64
    const auto prologueSummary = AnalyzeArm64Prologue(minStart, prologSize, maxEnd);
    
    if (Log::IsDebugEnabled())
    {
        Log::Debug("JITCompilationFinished: fpOff=", prologueSummary.SavedFpOffset, 
                   " lrOff=", prologueSummary.SavedLrOffset,
                   " prologLen=", prologueSummary.PrologLength);
    }
#endif

    if (Log::IsDebugEnabled())
    {
        LogJitMetadata(
            functionId,
            moduleId,
            classId,
            minStart,
            maxEnd,
            totalSize,
            rangeCount,
            ilMapCount,
            prologSize,
            TRUE, // safeToBlock,
            frameDisplay,
            moduleDisplay);
#ifdef ARM64
        Log::Debug(
            "JITCompilationFinished: frameSize=",
            prologueSummary.FrameSize,
            " savedRegs=",
            static_cast<uint32_t>(prologueSummary.SavedRegisterCount),
            " calleeMask=0x",
            std::hex,
            prologueSummary.CalleeSavedMask,
            std::dec);
#endif
    }

    JitCodeCache::MethodInfo methodInfo{};
    methodInfo.Start = minStart;
    methodInfo.End = maxEnd;
    methodInfo.Function = functionId;
    methodInfo.Module = moduleId;
    methodInfo.Class = classId;
    methodInfo.PrologSize = prologSize;
    methodInfo.RangeCount = rangeCount;
#ifdef ARM64
    methodInfo.FrameSize = prologueSummary.FrameSize;
    methodInfo.SavedFpOffset = prologueSummary.SavedFpOffset;
    methodInfo.SavedLrOffset = prologueSummary.SavedLrOffset;
    methodInfo.CalleeSavedRegisterMask = prologueSummary.CalleeSavedMask;
    methodInfo.SavedRegisterCount = prologueSummary.SavedRegisterCount;
    methodInfo.PrologLength = prologueSummary.PrologLength;

    if (methodInfo.SavedRegisterCount > 0)
    {
        std::copy_n(
            prologueSummary.Registers.begin(),
            methodInfo.SavedRegisterCount,
            methodInfo.SavedRegisters.begin());
        std::copy_n(
            prologueSummary.Offsets.begin(),
            methodInfo.SavedRegisterCount,
            methodInfo.SavedRegisterOffsets.begin());
    }

    if (methodInfo.PrologLength > 0)
    {
        std::copy_n(
            prologueSummary.PrologBytes.begin(),
            methodInfo.PrologLength,
            methodInfo.PrologBytes.begin());
    }
#endif

    RegisterMethodImpl2(methodInfo);
}

bool JitCodeCache::TryGetFunctionIdentity(
    FunctionID functionId,
    ClassID& classId,
    ModuleID& moduleId,
    mdToken& methodToken)
{
    const HRESULT result = _profilerInfo->GetFunctionInfo(functionId, &classId, &moduleId, &methodToken);
    if (SUCCEEDED(result))
    {
        return true;
    }

    if (Log::IsDebugEnabled())
    {
        Log::Debug(
            "JITCompilationFinished: GetFunctionInfo failed for functionId=0x",
            std::hex,
            functionId,
            " (hr=0x",
            result,
            ").");
    }
    return false;
}

bool JitCodeCache::TryComputeCodeLayout(
    FunctionID functionId,
    uintptr_t& minStart,
    uintptr_t& maxEnd,
    size_t& totalSize,
    ULONG32& actualRangeCount)
{
    ULONG32 rangeCount = 0;
    HRESULT result = _profilerInfo->GetCodeInfo2(functionId, 0, &rangeCount, nullptr);
    if (FAILED(result) || rangeCount == 0)
    {
        if (Log::IsDebugEnabled())
        {
            Log::Debug(
                "JITCompilationFinished: GetCodeInfo2 (count) failed for functionId=0x",
                std::hex,
                functionId,
                " (hr=0x",
                result,
                ").");
        }
        return false;
    }

    std::vector<COR_PRF_CODE_INFO> ranges(rangeCount);
    result = _profilerInfo->GetCodeInfo2(functionId, rangeCount, &rangeCount, ranges.data());
    if (FAILED(result) || rangeCount == 0)
    {
        if (Log::IsDebugEnabled())
        {
            Log::Debug(
                "JITCompilationFinished: GetCodeInfo2 (populate) failed for functionId=0x",
                std::hex,
                functionId,
                " (hr=0x",
                result,
                ").");
        }
        return false;
    }

    ranges.resize(rangeCount);

    uintptr_t startAccumulator = std::numeric_limits<uintptr_t>::max();
    uintptr_t endAccumulator = 0;
    size_t sizeAccumulator = 0;

    for (const auto& range : ranges)
    {
        const uintptr_t start = reinterpret_cast<uintptr_t>(range.startAddress);
        const uintptr_t end = start + static_cast<uintptr_t>(range.size);
        startAccumulator = std::min(startAccumulator, start);
        endAccumulator = std::max(endAccumulator, end);
        sizeAccumulator += static_cast<size_t>(range.size);
    }

    if (startAccumulator == std::numeric_limits<uintptr_t>::max() || endAccumulator <= startAccumulator)
    {
        return false;
    }

    minStart = startAccumulator;
    maxEnd = endAccumulator;
    totalSize = sizeAccumulator;
    actualRangeCount = rangeCount;
    return true;
}

ULONG32 JitCodeCache::ComputePrologSize(FunctionID functionId, ULONG32& ilMapCountOut)
{
    ilMapCountOut = 0;

    ULONG32 ilMapCount = 0;
    HRESULT result = _profilerInfo->GetILToNativeMapping(functionId, 0, &ilMapCount, nullptr);
    if (FAILED(result) || ilMapCount == 0)
    {
        return 0;
    }

    std::vector<COR_DEBUG_IL_TO_NATIVE_MAP> ilMap(ilMapCount);
    result = _profilerInfo->GetILToNativeMapping(functionId, ilMapCount, &ilMapCount, ilMap.data());
    if (FAILED(result) || ilMapCount == 0)
    {
        return 0;
    }

    ilMapCountOut = ilMapCount;

    for (const auto& entry : ilMap)
    {
        if (entry.ilOffset == CorDebugIlToNativeMappingTypes::PROLOG &&
            entry.nativeEndOffset > entry.nativeStartOffset)
        {
            return entry.nativeEndOffset - entry.nativeStartOffset;
        }
    }

    return 0;
}

void JitCodeCache::ResolveFrameDisplay(
    uintptr_t minStart,
    std::string& frameDisplay,
    std::string& moduleDisplay)
{
    if (minStart == 0)
    {
        return;
    }

    const auto [found, frameInfo] = _frameStore->GetFrame(minStart);
    if (found)
    {
        frameDisplay.assign(frameInfo.Frame);
        moduleDisplay.assign(frameInfo.ModuleName);
    }
}


int32_t DecodeSignedOffsetScale8(uint32_t instruction)
{
    return (((static_cast<int32_t>(instruction)) << 10) >> 25) * static_cast<int32_t>(sizeof(uintptr_t));
}

bool DecodeSubSpInstruction(uint32_t instruction, uint32_t& frameSizeBytes)
{
    // ADD/SUB (immediate): bits[28:24] == 10001
    if (((instruction >> 24) & 0x1fu) != 0x11u)
    {
        return false;
    }

    const uint32_t sf = (instruction >> 31) & 0x1u;
    const uint32_t op = (instruction >> 30) & 0x1u;
    const uint32_t setFlags = (instruction >> 29) & 0x1u;
    const uint32_t rn = (instruction >> 5) & 0x1fu;
    const uint32_t rd = instruction & 0x1fu;

    // We only care about `sub sp, sp, #imm` (64-bit, op=1, S=0, rn=sp, rd=sp)
    if (sf != 1u || op != 1u || setFlags != 0u || rn != 31u || rd != 31u)
    {
        return false;
    }

    const uint32_t shift = (instruction >> 22) & 0x3u;
    if (shift > 1u)
    {
        return false;
    }

    const uint32_t imm12 = (instruction >> 10) & 0xfffu;
    const uint32_t imm = imm12 << (shift == 0u ? 0u : 12u);
    frameSizeBytes = imm;
    return imm != 0u;
}

bool DecodeStorePairInstruction(uint32_t instruction, uint32_t& rt, uint32_t& rt2, uint32_t& rn, int32_t& offsetBytes)
{
    // Only handle 64-bit register pairs (opc == 0b10) and store variants (bit 22 == 0)
    if (((instruction >> 30) & 0x3u) != 0x2u)
    {
        return false;
    }

    if ((instruction & (1u << 22)) != 0)
    {
        return false;
    }

    rt = instruction & 0x1fu;
    rt2 = (instruction >> 10) & 0x1fu;
    rn = (instruction >> 5) & 0x1fu;
    offsetBytes = DecodeSignedOffsetScale8(instruction);
    return true;
}

Arm64PrologueSummary AnalyzeArm64Prologue(uintptr_t start, size_t prologSize, uintptr_t end)
{
    Arm64PrologueSummary summary{};

    if (start == 0 || end <= start)
    {
        return summary;
    }

    const size_t available = end - start;
    const size_t desired = prologSize != 0 ? prologSize : JitCodeCache::MethodInfo::MaxRecordedBytes;
    const size_t captureSize = std::min({available, desired, static_cast<size_t>(JitCodeCache::MethodInfo::MaxRecordedBytes)});

    summary.PrologLength = static_cast<uint16_t>(captureSize);
    if (captureSize > 0)
    {
        std::memcpy(summary.PrologBytes.data(), reinterpret_cast<const void*>(start), captureSize);
    }

    const size_t instructionCount = std::min<size_t>(captureSize / sizeof(uint32_t), 32);
    const auto* code = reinterpret_cast<const uint32_t*>(start);

    bool frameCaptured = false;
    uint32_t frameSizeCandidate = 0;

    for (size_t i = 0; i < instructionCount; i++)
    {
        const uint32_t instruction = code[i];
        uint32_t rt = 0;
        uint32_t rt2 = 0;
        uint32_t rn = 0;
        int32_t offsetBytes = 0;

        uint32_t subAmount = 0;
        if (DecodeSubSpInstruction(instruction, subAmount))
        {
            frameSizeCandidate = std::max(frameSizeCandidate, subAmount);
            summary.FrameSize = std::max(summary.FrameSize, subAmount);
            continue;
        }

        if (DecodeStorePairInstruction(instruction, rt, rt2, rn, offsetBytes) && rn == 31)
        {
            if (!frameCaptured && rt == 29 && rt2 == 30)
            {
                if (offsetBytes < 0)
                {
                    summary.FrameSize = std::max(summary.FrameSize, static_cast<uint32_t>(-offsetBytes));
                    summary.SavedFpOffset = 0;
                    summary.SavedLrOffset = static_cast<int32_t>(sizeof(uintptr_t));
                }
                else
                {
                    if (summary.FrameSize == 0 && frameSizeCandidate != 0)
                    {
                        summary.FrameSize = frameSizeCandidate;
                    }
                    summary.FrameSize = std::max(summary.FrameSize, static_cast<uint32_t>(offsetBytes + static_cast<int32_t>(2 * sizeof(uintptr_t))));
                    summary.SavedFpOffset = offsetBytes;
                    summary.SavedLrOffset = offsetBytes + static_cast<int32_t>(sizeof(uintptr_t));
                }

                frameCaptured = true;
                continue;
            }

            if (offsetBytes >= 0)
            {
                auto recordRegister = [&](uint32_t reg, int32_t relativeOffset) {
                    if (summary.SavedRegisterCount >= JitCodeCache::MethodInfo::MaxSavedRegisters)
                    {
                        return;
                    }

                    summary.Registers[summary.SavedRegisterCount] = static_cast<uint8_t>(reg);
                    summary.Offsets[summary.SavedRegisterCount] = static_cast<int16_t>(relativeOffset);
                    summary.SavedRegisterCount++;

                    if (reg >= 19 && reg <= 28)
                    {
                        summary.CalleeSavedMask |= 1u << (reg - 19);
                    }
                };

                recordRegister(rt, offsetBytes);
                recordRegister(rt2, offsetBytes + static_cast<int32_t>(sizeof(uintptr_t)));

                summary.FrameSize = std::max(
                    summary.FrameSize,
                    static_cast<uint32_t>(offsetBytes + static_cast<int32_t>(2 * sizeof(uintptr_t))));
            }
            else
            {
                summary.FrameSize = std::max(summary.FrameSize, static_cast<uint32_t>(-offsetBytes));
            }
        }
    }

    if (summary.FrameSize == 0 && frameSizeCandidate != 0)
    {
        summary.FrameSize = frameSizeCandidate;
    }

    if (summary.FrameSize != 0 && (summary.FrameSize % 16) != 0)
    {
        summary.FrameSize = (summary.FrameSize + 15) & ~15u;
    }

    return summary;
}


void LogJitMetadata(
    FunctionID functionId,
    ModuleID moduleId,
    ClassID classId,
    uintptr_t minStart,
    uintptr_t maxEnd,
    size_t totalSize,
    ULONG32 rangeCount,
    ULONG32 ilMapCount,
    ULONG32 prologSize,
    BOOL safeToBlock,
    const std::string& frameDisplay,
    const std::string& moduleDisplay)
{
    Log::Debug(
        "JITCompilationFinished: functionId=0x",
        std::hex,
        functionId,
        " moduleId=0x",
        moduleId,
        " classId=0x",
        classId,
        " start=0x",
        minStart,
        " end=0x",
        maxEnd,
        " size=",
        std::dec,
        totalSize,
        "B ranges=",
        rangeCount,
        " ilMap=",
        ilMapCount,
        " prologNativeSize=",
        prologSize,
        " safeToBlock=",
        safeToBlock,
        " frame=",
        frameDisplay,
        " module=",
        moduleDisplay);
}

bool JitCodeCache::IsManagedCode(uintptr_t ip) const
{
#ifdef LINUX
    if (const auto* methodInfo = FindMethod(ip))
    {
        return true;
    }
    else
#endif
    {
        auto* librariesCache = LibrariesInfoCache::GetInstance();
        if (librariesCache != nullptr)
        {
            return librariesCache->IsAddressInManagedRegion(ip);
        }
    }
    return false;
}