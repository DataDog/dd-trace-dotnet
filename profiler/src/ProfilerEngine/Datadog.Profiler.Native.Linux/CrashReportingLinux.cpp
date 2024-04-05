#include "CrashReportingLinux.h"

#include <algorithm>
#include <cstdint>
#include <vector>
#include <dirent.h>
#include <string>
#include <memory>

#include <libunwind.h>
#include <libunwind-ptrace.h>
#include <sys/ptrace.h>
#include <sys/wait.h>
#include <iostream>
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

std::unique_ptr<CrashReporting> CrashReporting::Create(int32_t pid, int32_t signal)
{
    return std::make_unique<CrashReportingLinux>(pid, signal);
}

CrashReportingLinux::CrashReportingLinux(int32_t pid, int32_t signal)
    : CrashReporting(pid, signal)
{
    _addressSpace = unw_create_addr_space(&_UPT_accessors, 0);
    _modules = GetModules();
}

CrashReportingLinux::~CrashReportingLinux()
{
    unw_destroy_addr_space(_addressSpace);
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

std::vector<StackFrame> CrashReportingLinux::GetThreadFrames(int32_t tid, ResolveManagedMethod resolveManagedMethod)
{
    std::vector<StackFrame> frames;

    auto context = _UPT_create(tid);

    unw_cursor_t cursor;

    auto result = unw_init_remote(&cursor, _addressSpace, context);

    if (result != 0)
    {
        std::cout << "Failed to initialize cursor: " << result << "\n";
        return frames;
    }

    unw_word_t ip;

    // Walk the stack
    do
    {
        unw_get_reg(&cursor, UNW_REG_IP, &ip);

        StackFrame stackFrame;
        stackFrame.ip = ip;

        ResolveMethodData methodData;

        auto resolved = resolveManagedMethod(ip, &methodData);

        if (resolved != 0)
        {
            // Not a managed method

            auto module = FindModule(ip);
            stackFrame.moduleAddress = module.second;

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
                else
                {
                    std::ostringstream unknownModule;
                    unknownModule << module.first << "!<unknown>+" << std::hex << (ip - module.second);
                    stackFrame.method = unknownModule.str();
                }
            }            
        }
        else if (resolved == 0)
        {
            stackFrame.method = std::string(methodData.symbolName);
            stackFrame.moduleAddress = methodData.moduleAddress;
            stackFrame.symbolAddress = methodData.symbolAddress;
        }

        frames.push_back(stackFrame);

    } while (unw_step(&cursor) > 0);

    _UPT_destroy(context);

    return frames;
}

std::string CrashReportingLinux::GetSignalInfo()
{
    auto signalInfo = strsignal(_signal);

    if (signalInfo == nullptr)
    {
        return {};
    }

    return signalInfo;
}

std::vector<int32_t> CrashReportingLinux::GetThreads()
{
    DIR* proc_dir;
    char dirname[256];

    std::string pidPath = (_pid == -1) ? "self" : std::to_string(_pid);
    snprintf(dirname, sizeof(dirname), "/proc/%s/task", pidPath.c_str());

    proc_dir = opendir(dirname);

    std::vector<int32_t> threads;

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
            threads.push_back(threadId);
        }

        closedir(proc_dir);
    }

    return threads;
}