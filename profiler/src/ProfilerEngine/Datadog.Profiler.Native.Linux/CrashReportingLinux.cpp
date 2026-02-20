// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CrashReportingLinux.h"

#include <algorithm>
#include <cstdint>
#include <dirent.h>
#include <memory>
#include <string>
#include <vector>

#include "FfiHelper.h"
#include <fstream>
#include <libunwind-ptrace.h>
#include <libunwind.h>
#include <map>
#include <sstream>
#include <string.h>
#include <sys/ptrace.h>
#include <sys/uio.h>
#include <sys/wait.h>

extern "C"
{
#include "datadog/common.h"
#include "datadog/crashtracker.h"
#include "datadog/profiling.h"
}

#include <shared/src/native-src/dd_filesystem.hpp>

CrashReporting* CrashReporting::Create(int32_t pid)
{
    auto crashReporting = new CrashReportingLinux(pid);
    return (CrashReporting*)crashReporting;
}

CrashReportingLinux::CrashReportingLinux(int32_t pid) :
    CrashReporting(pid)
{
}

CrashReportingLinux::~CrashReportingLinux()
{
    if (_addressSpace != nullptr)
    {
        unw_destroy_addr_space(_addressSpace);
    }
}

int32_t CrashReportingLinux::Initialize()
{
    auto result = CrashReporting::Initialize();

    if (result == 0)
    {
        _addressSpace = unw_create_addr_space(&_UPT_accessors, 0);

        if (_addressSpace == nullptr)
        {
            return 999;
        }

        _modules = GetModules();
    }

    return result;
}

const ModuleInfo* CrashReportingLinux::FindModule(uintptr_t ip)
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

std::vector<ModuleInfo> CrashReportingLinux::GetModules()
{
    std::vector<ModuleInfo> modules;
    std::map<std::string, uintptr_t> moduleBaseAddresses;
    std::ifstream mapsFile("/proc/" + std::to_string(_pid) + "/maps", std::ifstream::in);
    std::string line;

    while (std::getline(mapsFile, line))
    {
        std::istringstream iss(line);
        std::string addressRange, permissions, offset, dev, inode;
        std::string path;

        iss >> addressRange >> permissions >> offset >> dev >> inode;
        std::getline(iss, path); // Skip whitespace at the start

        // Trim path
        path.erase(path.begin(), std::find_if(path.begin(), path.end(), [](int ch) {
                       return !std::isspace(ch);
                   }));

        if (path.empty())
        {
            continue;
        }

        size_t dashPos = addressRange.find('-');

        if (dashPos == std::string::npos)
        {
            continue;
        }

        auto startStr = std::string_view(addressRange).substr(0, dashPos);
        auto endStr = std::string_view(addressRange).substr(dashPos + 1);
        uintptr_t start = std::stoull(startStr.data(), nullptr, 16);
        uintptr_t end = std::stoull(endStr.data(), nullptr, 16);

        // Get the base address of the module if we have it, otherwise add it
        auto it = moduleBaseAddresses.find(path);
        uintptr_t baseAddress;

        if (it != moduleBaseAddresses.end())
        {
            baseAddress = it->second;
        }
        else
        {
            baseAddress = start;
            moduleBaseAddresses[path] = baseAddress;
        }

        auto buildId = BuildId::From(path.data());
        modules.push_back(ModuleInfo{ start, end, baseAddress, std::move(path), std::move(buildId) });
    }

    return modules;
}

#define SET_REG(cursor, reg, value, default) \
do {\
    if (unw_set_reg(&cursor, reg, value) != 0) \
    {\
        return {default};\
    }\
} while (0);

std::optional<unw_cursor_t> create_cursor_from_context(uint32_t pid, unw_addr_space_t& addressSpace, void* libunwindContext, void* threadContext)
{
    unw_cursor_t cursor;
    if (unw_init_remote(&cursor, addressSpace, threadContext) != 0)
    {
        return std::nullopt;
    }

    if (threadContext == nullptr)
    {
        return {cursor};
    }

    ucontext_t remoteCtx;
    // threadContext is a pointer in the remote process's address space
    // read it via process_vm_readv (more efficient than ptrace for bulk reads)
    struct iovec local  = { .iov_base = &remoteCtx, .iov_len = sizeof(remoteCtx) };
    struct iovec remote = { .iov_base = threadContext, .iov_len = sizeof(remoteCtx) };

    if (process_vm_readv(pid, &local, 1, &remote, 1, 0) == sizeof(remoteCtx)) {
        unw_cursor_t newCursor = cursor;

        // Override cursor with the register state from the crash point.
        // This skips the signal frame entirely, which is required on
        // musl/Alpine where libunwind cannot unwind past the signal trampoline.
#if defined(AMD64)
        SET_REG(newCursor, UNW_REG_IP, remoteCtx.uc_mcontext.gregs[REG_RIP], cursor);
        SET_REG(newCursor, UNW_REG_SP, remoteCtx.uc_mcontext.gregs[REG_RSP], cursor);
        SET_REG(newCursor, UNW_X86_64_RBP, remoteCtx.uc_mcontext.gregs[REG_RBP], cursor);
        // Callee-saved registers (DWARF unwind rules may reference these)
        SET_REG(newCursor, UNW_X86_64_RBX, remoteCtx.uc_mcontext.gregs[REG_RBX], cursor);
        SET_REG(newCursor, UNW_X86_64_R12, remoteCtx.uc_mcontext.gregs[REG_R12], cursor);
        SET_REG(newCursor, UNW_X86_64_R13, remoteCtx.uc_mcontext.gregs[REG_R13], cursor);
        SET_REG(newCursor, UNW_X86_64_R14, remoteCtx.uc_mcontext.gregs[REG_R14], cursor);
        SET_REG(newCursor, UNW_X86_64_R15, remoteCtx.uc_mcontext.gregs[REG_R15], cursor);
#elif defined(ARM64)
        SET_REG(newCursor, UNW_REG_IP, remoteCtx.uc_mcontext.pc, cursor);
        SET_REG(newCursor, UNW_REG_SP, remoteCtx.uc_mcontext.sp, cursor);
        SET_REG(newCursor, UNW_AARCH64_X29, remoteCtx.uc_mcontext.regs[29], cursor); // FP
        SET_REG(newCursor, UNW_AARCH64_X30, remoteCtx.uc_mcontext.regs[30], cursor); // LR
        // Callee-saved registers (DWARF unwind rules may reference these)
        SET_REG(newCursor, UNW_AARCH64_X19, remoteCtx.uc_mcontext.regs[19], cursor);
        SET_REG(newCursor, UNW_AARCH64_X20, remoteCtx.uc_mcontext.regs[20], cursor);
        SET_REG(newCursor, UNW_AARCH64_X21, remoteCtx.uc_mcontext.regs[21], cursor);
        SET_REG(newCursor, UNW_AARCH64_X22, remoteCtx.uc_mcontext.regs[22], cursor);
        SET_REG(newCursor, UNW_AARCH64_X23, remoteCtx.uc_mcontext.regs[23], cursor);
        SET_REG(newCursor, UNW_AARCH64_X24, remoteCtx.uc_mcontext.regs[24], cursor);
        SET_REG(newCursor, UNW_AARCH64_X25, remoteCtx.uc_mcontext.regs[25], cursor);
        SET_REG(newCursor, UNW_AARCH64_X26, remoteCtx.uc_mcontext.regs[26], cursor);
        SET_REG(newCursor, UNW_AARCH64_X27, remoteCtx.uc_mcontext.regs[27], cursor);
        SET_REG(newCursor, UNW_AARCH64_X28, remoteCtx.uc_mcontext.regs[28], cursor);
#else
#error "Unsupported architecture"
#endif
        return {newCursor};
    }
    return {cursor};
}

std::vector<StackFrame> CrashReportingLinux::GetThreadFrames(int32_t tid, void* threadContext, ResolveManagedCallstack resolveManagedCallstack, void* context)
{
    std::vector<StackFrame> frames;

    auto libunwindContext = _UPT_create(tid);

    auto cursorOpt = create_cursor_from_context(_pid, _addressSpace, libunwindContext, threadContext);
    if (!cursorOpt.has_value())
    {
        return frames;
    }

    auto cursor = cursorOpt.value();

    // Get the managed callstack
    ResolveMethodData* managedCallstack;
    int32_t numberOfManagedFrames;

    auto resolved = resolveManagedCallstack(tid, context, &managedCallstack, &numberOfManagedFrames);

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

    unw_word_t ip;
    unw_word_t sp;

    // Walk the stack
    do
    {
        unw_get_reg(&cursor, UNW_REG_IP, &ip);
        unw_get_reg(&cursor, UNW_REG_SP, &sp);

        StackFrame stackFrame{};
        stackFrame.ip = ip;
        stackFrame.sp = sp;
        stackFrame.isSuspicious = false;


        auto* module = FindModule(ip);
        if (module != nullptr)
        {
            stackFrame.moduleAddress = module->baseAddress;

            bool hasName = false;

            unw_proc_info_t procInfo;
            auto result = unw_get_proc_info(&cursor, &procInfo);

            if (result == 0)
            {
                stackFrame.symbolAddress = procInfo.start_ip;

                ResolveMethodData methodData;
                unw_word_t offset;
                result = unw_get_proc_name(&cursor, methodData.symbolName, sizeof(methodData.symbolName), &offset);

                if (result == 0)
                {
                    stackFrame.method = std::string(methodData.symbolName);
                    hasName = true;

                    auto demangleResult = ddog_crasht_demangle(libdatadog::to_char_slice(stackFrame.method), DDOG_CRASHT_DEMANGLE_OPTIONS_COMPLETE);

                    if (demangleResult.tag == DDOG_STRING_WRAPPER_RESULT_OK)
                    {
                        auto stringWrapper = demangleResult.ok;

                        if (stringWrapper.message.len > 0)
                        {
                            stackFrame.method = std::string((char*)stringWrapper.message.ptr, stringWrapper.message.len);
                        }
                        ddog_StringWrapper_drop(&demangleResult.ok);
                    }
                    else
                    {
                        SetLastError(demangleResult.err);
                    }
                }
            }

            if (!hasName)
            {
                std::ostringstream unknownModule;
                unknownModule << module->path << "!<unknown>+" << std::hex << (ip - module->baseAddress);
                stackFrame.method = unknownModule.str();
            }

            stackFrame.isSuspicious = false;

            stackFrame.buildId = module->build_id;
            stackFrame.modulePath = module->path;

            fs::path modulePath(module->path);

            if (modulePath.has_filename())
            {
                const auto moduleFilename = modulePath.stem().string();

                if ((moduleFilename.rfind("Datadog", 0) == 0 && stackFrame.method != "dd_pthread_entry")
                    || moduleFilename == "libdatadog"
                    || moduleFilename == "datadog"
                    || moduleFilename == "libddwaf"
                    || moduleFilename == "ddwaf" )
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

    } while (unw_step(&cursor) > 0);

    _UPT_destroy(libunwindContext);

    return MergeFrames(frames, managedFrames);
}

std::string CrashReportingLinux::GetSignalInfo(int32_t signal)
{
    auto signalInfo = strsignal(signal);

    if (signalInfo == nullptr)
    {
        return {};
    }

    return signalInfo;
}

std::vector<std::pair<int32_t, std::string>> CrashReportingLinux::GetThreads()
{
    DIR* proc_dir;
    char dirname[256];

    std::string pidPath = (_pid == -1) ? "self" : std::to_string(_pid);
    snprintf(dirname, sizeof(dirname), "/proc/%s/task", pidPath.c_str());

    proc_dir = opendir(dirname);

    std::vector<std::pair<int32_t, std::string>> threads;

    if (proc_dir != nullptr)
    {
        threads.reserve(512);

        /* /proc available, iterate through tasks... */
        struct dirent* entry;
        while ((entry = readdir(proc_dir)) != nullptr)
        {
            if (entry->d_name[0] == '.')
                continue;
            auto threadId = atoi(entry->d_name);
            auto threadName = GetThreadName(threadId);
            threads.push_back(std::make_pair(threadId, std::move(threadName)));
        }

        closedir(proc_dir);
    }

    return threads;
}

std::string CrashReportingLinux::GetThreadName(int32_t tid)
{
    char path[256];
    snprintf(path, sizeof(path), "/proc/%d/task/%d/comm", _pid, tid);
    std::ifstream commFile(path);
    if (!commFile.is_open())
    {
        return "";
    }

    std::string threadName;
    std::getline(commFile, threadName);
    commFile.close();
    return threadName;
}