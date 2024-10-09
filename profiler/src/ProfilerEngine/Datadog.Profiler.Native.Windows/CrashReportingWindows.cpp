// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "windows.h"

#include "CrashReportingWindows.h"
#include "TlHelp32.h"
#include "DbgHelp.h"
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
                    std::string threadName;
                    PWSTR description;
                    
                    if (SUCCEEDED(GetThreadDescription(thread, &description)))
                    {
                        threadName = shared::ToString(description);
                    }

                    threads.push_back({ threadEntry.th32ThreadID, std::move(threadName) });
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

            StackFrame stackFrame;
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
            auto [moduleName, moduleAddress] = FindModule(nativeStackFrame.AddrPC.Offset);

            StackFrame stackFrame;
            stackFrame.ip = nativeStackFrame.AddrPC.Offset;
            stackFrame.sp = nativeStackFrame.AddrStack.Offset;
            stackFrame.isSuspicious = false;
            stackFrame.moduleAddress = moduleAddress;
            stackFrame.symbolAddress = nativeStackFrame.AddrPC.Offset;

            std::ostringstream methodName;
            methodName << moduleName << "!<unknown>+" << std::hex << (nativeStackFrame.AddrPC.Offset - moduleAddress);
            stackFrame.method = methodName.str();

            fs::path modulePath(moduleName);

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

                modules.push_back({ (uintptr_t)moduleInfo.lpBaseOfDll, (uintptr_t)moduleInfo.lpBaseOfDll + moduleInfo.SizeOfImage, std::move(resolvedModuleName) });
            }
        }
    }

    return modules;
}

std::pair<std::string_view, uintptr_t> CrashReportingWindows::FindModule(uintptr_t ip)
{
    for (auto const& module : _modules)
    {
        if (ip >= module.startAddress && ip < module.endAddress)
        {
            return std::make_pair(module.path, module.startAddress);
        }
    }

    return std::make_pair("", 0);
}
