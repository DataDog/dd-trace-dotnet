// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "windows.h"

#include "CrashReportingWindows.h"
#include "TlHelp32.h"
#include "DbgHelp.h"
#include "OpSysTools.h"
#include "Psapi.h"

#include <shared/src/native-src/string.h>
#include <shared/src/native-src/dd_filesystem.hpp>

#pragma comment(lib, "dbghelp.lib")

CrashReporting* CrashReporting::Create(int32_t pid)
{
    auto crashReporting = new CrashReportingWindows(pid);
    return (CrashReporting*)crashReporting;
}

CrashReportingWindows::CrashReportingWindows(int32_t pid)
    : CrashReporting(pid)
    , _process(NULL)
    , _readMemory(nullptr)
{
}

CrashReportingWindows::~CrashReportingWindows() = default;

int32_t CrashReportingWindows::Initialize()
{
    auto result = CrashReporting::Initialize();

    if (result == 0)
    {
        _process = ScopedHandle(OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, _pid));

        if (!_process.IsValid())
        {
            return 1;
        }

        SetMemoryReader([this](uintptr_t address, SIZE_T size) { return ReadRemoteMemory(_process, address, size); });

        SymInitialize(_process, nullptr, TRUE);

        _modules = GetModules();
    }

    return result;
}

std::vector<std::pair<int32_t, std::string>> CrashReportingWindows::GetThreads()
{
    std::vector<std::pair<int32_t, std::string>> threads;

    auto threadSnapshot = ScopedHandle(CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, _pid));

    if (!threadSnapshot.IsValid())
    {
        return threads;
    }

    THREADENTRY32 threadEntry = {};
    threadEntry.dwSize = sizeof(THREADENTRY32);

    if (Thread32First(threadSnapshot, &threadEntry))
    {
        do
        {
            if (threadEntry.th32OwnerProcessID == _pid)
            {
                auto thread = ScopedHandle(OpenThread(THREAD_QUERY_INFORMATION, FALSE, threadEntry.th32ThreadID));

                if (thread.IsValid())
                {
                    auto wThreadName = OpSysTools::GetNativeThreadName(thread);
                    threads.push_back({ threadEntry.th32ThreadID, shared::ToString(wThreadName) });
                }
            }
        } while (Thread32Next(threadSnapshot, &threadEntry));
    }

    return threads;
}

std::vector<StackFrame> CrashReportingWindows::GetThreadFrames(int32_t tid, ResolveManagedCallstack resolveManagedCallstack, void* callbackContext)
{
    std::vector<StackFrame> frames;

    // Get the managed callstack
    ResolveMethodData* managedCallstack;
    int32_t numberOfManagedFrames;

    auto resolved = resolveManagedCallstack(tid, callbackContext, &managedCallstack, &numberOfManagedFrames);

    std::vector<StackFrame> managedFrames;

    if (resolved == 0 && numberOfManagedFrames > 0)
    {
        managedFrames.reserve(numberOfManagedFrames);

        for (int i = 0; i < numberOfManagedFrames; i++)
        {
            auto const& managedFrame = managedCallstack[i];

            StackFrame stackFrame{};
            stackFrame.ip = managedFrame.ip;
            stackFrame.sp = managedFrame.sp;
            stackFrame.method = std::string(managedFrame.symbolName);
            stackFrame.moduleAddress = managedFrame.moduleAddress;
            stackFrame.symbolAddress = managedFrame.symbolAddress;
            stackFrame.isSuspicious = managedFrame.isSuspicious;

            managedFrames.push_back(std::move(stackFrame));
        }
    }

    CONTEXT context = {};
    context.ContextFlags = CONTEXT_FULL;

    auto thread = ScopedHandle(OpenThread(THREAD_GET_CONTEXT, FALSE, tid));

    if (!thread.IsValid())
    {
        return managedFrames;
    }

    if (GetThreadContext(thread, &context))
    {
        STACKFRAME_EX nativeStackFrame = {};
#ifdef _M_X64
        int machineType = IMAGE_FILE_MACHINE_AMD64;
        nativeStackFrame.AddrPC.Offset = context.Rip;
        nativeStackFrame.AddrPC.Mode = AddrModeFlat;
        nativeStackFrame.AddrFrame.Offset = context.Rsp;
        nativeStackFrame.AddrFrame.Mode = AddrModeFlat;
        nativeStackFrame.AddrStack.Offset = context.Rsp;
        nativeStackFrame.AddrStack.Mode = AddrModeFlat;
#elif _M_IX86
        int machineType = IMAGE_FILE_MACHINE_I386;
        nativeStackFrame.AddrPC.Offset = context.Eip;
        nativeStackFrame.AddrPC.Mode = AddrModeFlat;
        nativeStackFrame.AddrFrame.Offset = context.Ebp;
        nativeStackFrame.AddrFrame.Mode = AddrModeFlat;
        nativeStackFrame.AddrStack.Offset = context.Esp;
        nativeStackFrame.AddrStack.Mode = AddrModeFlat;
#endif

        while (StackWalkEx(machineType,
            _process,
            thread,
            &nativeStackFrame,
            &context,
            nullptr,
            nullptr,
            nullptr,
            nullptr,
            SYM_STKWALK_DEFAULT))
        {
            auto module = FindModule(nativeStackFrame.AddrPC.Offset);

            StackFrame stackFrame{};
            stackFrame.ip = nativeStackFrame.AddrPC.Offset;
            stackFrame.sp = nativeStackFrame.AddrStack.Offset;
            stackFrame.isSuspicious = false;
            stackFrame.symbolAddress = nativeStackFrame.AddrPC.Offset;

            if (module != nullptr)
            {
                stackFrame.moduleAddress = module->startAddress;
                stackFrame.buildId = module->buildId;
                stackFrame.modulePath = module->path;

                std::ostringstream methodName;
                methodName << module->path << "!<unknown>+" << std::hex << (nativeStackFrame.AddrPC.Offset - module->startAddress);
                stackFrame.method = methodName.str();

                fs::path modulePath(module->path);

                if (modulePath.has_filename())
                {
                    const auto moduleFilename = modulePath.stem().string();

                    if (moduleFilename.rfind("Datadog", 0) == 0
                        || moduleFilename == "libdatadog"
                        || moduleFilename == "datadog"
                        || moduleFilename == "libddwaf"
                        || moduleFilename == "ddwaf")
                    {
                        stackFrame.isSuspicious = true;
                    }
                }
            }
            else
            {
                stackFrame.method = "<unknown>";
            }

            frames.push_back(std::move(stackFrame));
        }
    }

    return MergeFrames(frames, managedFrames);
}

std::string CrashReportingWindows::GetSignalInfo(int32_t signal)
{
    return std::string();
}

std::vector<ModuleInfo> CrashReportingWindows::GetModules()
{
    std::vector<ModuleInfo> modules;

    HMODULE hModules[1024];
    DWORD cbNeeded;
    if (EnumProcessModules(_process, hModules, sizeof(hModules), &cbNeeded))
    {
        for (unsigned int i = 0; i < (cbNeeded / sizeof(HMODULE)); ++i)
        {
            MODULEINFO moduleInfo = {};
            if (GetModuleInformation(_process, hModules[i], &moduleInfo, sizeof(moduleInfo)))
            {
                std::string resolvedModuleName = "<unknown>";

                char moduleName[MAX_PATH];
                if (GetModuleFileNameExA(_process, hModules[i], moduleName, sizeof(moduleName)))
                {
                    resolvedModuleName = moduleName;
                }

                auto buildId = ExtractBuildId((uintptr_t)moduleInfo.lpBaseOfDll);

                ModuleInfo module{(uintptr_t)moduleInfo.lpBaseOfDll, (uintptr_t)moduleInfo.lpBaseOfDll + moduleInfo.SizeOfImage, std::move(resolvedModuleName), std::move(buildId)};
                modules.push_back(std::move(module));
            }
        }
    }

    return modules;
}

const ModuleInfo* CrashReportingWindows::FindModule(uintptr_t ip)
{
    for (auto const& module : _modules)
    {
        if (ip >= module.startAddress && ip < module.endAddress)
        {
            return &module;
        }
    }

    return nullptr;
}

std::vector<BYTE> CrashReportingWindows::ReadRemoteMemory(HANDLE process, uintptr_t address, SIZE_T size)
{
    std::vector<BYTE> buffer(size);
    SIZE_T bytesRead = 0;

    if (ReadProcessMemory(process, reinterpret_cast<LPCVOID>(address), buffer.data(), size, &bytesRead) && bytesRead == size)
    {
        return buffer;
    }

    return {};
}

BuildId CrashReportingWindows::ExtractBuildId(uintptr_t baseAddress)
{
    // Read the DOS header
    auto dosHeaderBuffer = _readMemory(baseAddress, sizeof(IMAGE_DOS_HEADER));
    if (dosHeaderBuffer.empty())
    {
        return {};
    }

    auto dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(dosHeaderBuffer.data());

    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
    {
        return {};
    }

    // Read the NT headers
    uintptr_t ntHeadersAddress = baseAddress + dosHeader->e_lfanew;
    auto ntHeadersBuffer = _readMemory(ntHeadersAddress, sizeof(IMAGE_NT_HEADERS_GENERIC));
    if (ntHeadersBuffer.empty())
    {
        return {};
    }

    auto ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS_GENERIC*>(ntHeadersBuffer.data());

    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE)
    {
        return {};
    }

    // Check the PE type
    bool isPE32 = (ntHeaders->Magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC);
    bool isPE64 = (ntHeaders->Magic == IMAGE_NT_OPTIONAL_HDR64_MAGIC);

    if (!isPE32 && !isPE64)
    {
        return {};
    }

    // Read the debug directory according to the PE type
    IMAGE_DATA_DIRECTORY debugDataDir;

    if (isPE32)
    {
        auto header32Buffer = _readMemory(ntHeadersAddress, sizeof(IMAGE_NT_HEADERS32));
        if (header32Buffer.empty())
        {
            return {};
        }

        auto header32 = reinterpret_cast<IMAGE_NT_HEADERS32*>(header32Buffer.data());
        debugDataDir = header32->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_DEBUG];
    }
    else
    {
        auto header64Buffer = _readMemory(ntHeadersAddress, sizeof(IMAGE_NT_HEADERS64));
        if (header64Buffer.empty())
        {
            return {};
        }

        auto header64 = reinterpret_cast<IMAGE_NT_HEADERS64*>(header64Buffer.data());
        debugDataDir = header64->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_DEBUG];
    }

    uintptr_t debugDirectoryAddress = baseAddress + debugDataDir.VirtualAddress;
    if (debugDirectoryAddress == 0)
    {
        return {};
    }

    auto debugDirectoryBuffer = _readMemory(debugDirectoryAddress, debugDataDir.Size);
    if (debugDirectoryBuffer.empty())
    {
        return {};
    }

    auto debugDirectory = reinterpret_cast<PIMAGE_DEBUG_DIRECTORY>(debugDirectoryBuffer.data());

    // Iterate over the debug directory entries and look for IMAGE_DEBUG_TYPE_CODEVIEW entries
    for (size_t i = 0; i < debugDataDir.Size / sizeof(IMAGE_DEBUG_DIRECTORY); i++)
    {
        if (debugDirectory[i].Type == IMAGE_DEBUG_TYPE_CODEVIEW)
        {
            struct CV_INFO_PDB70
            {
                DWORD Signature;
                GUID Guid;
                DWORD Age;
                char PdbFileName[];
            };

            // Extract the PDB info from the codeview entry
            auto pdbInfoAddress = baseAddress + debugDirectory[i].AddressOfRawData;
            auto pdbInfoBuffer = _readMemory(pdbInfoAddress, sizeof(CV_INFO_PDB70));

            if (pdbInfoBuffer.empty())
            {
                return {};
            }

            auto pdbInfo = reinterpret_cast<CV_INFO_PDB70*>(pdbInfoBuffer.data());

            constexpr DWORD PDB70_SIGNATURE = 0x53445352; // "SDSR"

            if (pdbInfo->Signature == PDB70_SIGNATURE)
            {
                return BuildId::From(pdbInfo->Guid, pdbInfo->Age);
            }
        }
    }

    return {};
}

void CrashReportingWindows::SetMemoryReader(std::function<std::vector<BYTE>(uintptr_t, SIZE_T)> readMemory)
{
    _readMemory = readMemory;
}
