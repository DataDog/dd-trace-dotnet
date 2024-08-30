// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CrashReportingLinux.h"

#include <algorithm>
#include <cstdint>
#include <vector>
#include <dirent.h>
#include <string>
#include <memory>
#include <filesystem>

#include <libunwind.h>
#include <libunwind-ptrace.h>
#include <sys/ptrace.h>
#include <sys/wait.h>
#include <fstream>
#include <sstream>
#include <map>
#include <string.h>
#include "FfiHelper.h"

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

CrashReporting* CrashReporting::Create(int32_t pid)
{
    auto crashReporting = new CrashReportingLinux(pid);
    return (CrashReporting*)crashReporting;
}

CrashReportingLinux::CrashReportingLinux(int32_t pid)
    : CrashReporting(pid)
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

std::pair<std::string, uintptr_t> CrashReportingLinux::FindModule(uintptr_t ip)
{
    for (auto& module : _modules)
    {
        if (ip >= module.startAddress && ip < module.endAddress)
        {
            return std::make_pair(module.path, module.baseAddress);
        }
    }

    return std::make_pair("", 0);
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
        path.erase(path.begin(), std::find_if(path.begin(), path.end(), [](int ch)
            {
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

        std::string startStr = addressRange.substr(0, dashPos);
        std::string endStr = addressRange.substr(dashPos + 1);
        uintptr_t start = std::stoull(startStr, nullptr, 16);
        uintptr_t end = std::stoull(endStr, nullptr, 16);

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

        modules.push_back(ModuleInfo{ start, end, baseAddress, path });
    }

    return modules;
}

std::vector<StackFrame> CrashReportingLinux::GetThreadFrames(int32_t tid, ResolveManagedCallstack resolveManagedCallstack, void* context)
{
    std::vector<StackFrame> frames;

    auto libunwindContext = _UPT_create(tid);

    unw_cursor_t cursor;

    auto result = unw_init_remote(&cursor, _addressSpace, libunwindContext);

    if (result != 0)
    {
        return frames;
    }

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

        StackFrame stackFrame;
        stackFrame.ip = ip;
        stackFrame.sp = sp;
        stackFrame.isSuspicious = false;

        ResolveMethodData methodData;

        auto module = FindModule(ip);
        stackFrame.moduleAddress = module.second;

        bool hasName = false;

        unw_proc_info_t procInfo;
        result = unw_get_proc_info(&cursor, &procInfo);

        if (result == 0)
        {
            stackFrame.symbolAddress = procInfo.start_ip;

            unw_word_t offset;
            result = unw_get_proc_name(&cursor, methodData.symbolName, sizeof(methodData.symbolName), &offset);

            if (result == 0)
            {
                stackFrame.method = std::string(methodData.symbolName);
                hasName = true;

                auto demangleResult = ddog_demangle(libdatadog::FfiHelper::StringToCharSlice(stackFrame.method), DDOG_PROF_DEMANGLE_OPTIONS_COMPLETE);

                if (demangleResult.tag == DDOG_PROF_STRING_WRAPPER_RESULT_OK)
                {
                    // TODO: There is currently no safe way to free the StringWrapper
                    auto stringWrapper = demangleResult.ok;

                    if (stringWrapper.message.len > 0)
                    {
                        stackFrame.method = std::string((char*)stringWrapper.message.ptr, stringWrapper.message.len);                        
                    }
                }
            }
        }

        if (!hasName)
        {
            std::ostringstream unknownModule;
            unknownModule << module.first << "!<unknown>+" << std::hex << (ip - module.second);
            stackFrame.method = unknownModule.str();
        }

        stackFrame.isSuspicious = false;

        std::filesystem::path modulePath(module.first);

        if (modulePath.has_filename())
        {
            const auto moduleFilename = modulePath.stem().string();

            if (moduleFilename.rfind("Datadog", 0) == 0
                || moduleFilename == "libdatadog"
                || moduleFilename == "datadog"
                || moduleFilename == "libddwaf"
                || moduleFilename == "ddwaf" )
            {
                stackFrame.isSuspicious = true;
            }
        }

        frames.push_back(std::move(stackFrame));

    } while (unw_step(&cursor) > 0);

    _UPT_destroy(libunwindContext);

    return MergeFrames(frames, managedFrames);
}

std::vector<StackFrame> CrashReportingLinux::MergeFrames(const std::vector<StackFrame>& nativeFrames, const std::vector<StackFrame>& managedFrames)
{
    std::vector<StackFrame> result;
    result.reserve(std::max(nativeFrames.size(), managedFrames.size()));

    size_t i = 0, j = 0;
    while (i < nativeFrames.size() && j < managedFrames.size())
    {
        if (nativeFrames.at(i).sp < managedFrames.at(j).sp)
        {
            result.push_back(nativeFrames.at(i));
            ++i;
        }
        else if (managedFrames.at(j).sp < nativeFrames.at(i).sp)
        {
            result.push_back(managedFrames.at(j));
            ++j;
        }
        else
        { // frames[i].sp == managedFrames[j].sp
            // Prefer managedFrame when sp values are the same
            result.push_back(managedFrames.at(j));
            ++i;
            ++j;
        }
    }

    // Add any remaining frames that are left in either vector
    while (i < nativeFrames.size())
    {
        result.push_back(nativeFrames.at(i));
        ++i;
    }

    while (j < managedFrames.size())
    {
        result.push_back(managedFrames.at(j));
        ++j;
    }

    return result;
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
            threads.push_back(std::make_pair(threadId, threadName));
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