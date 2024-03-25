#include "CrashReportingLinux.h"
#include <stdint.h>
#include <vector>
#include <dirent.h>
#include <string>

#include <libunwind.h>
#include <libunwind-ptrace.h>
#include <sys/ptrace.h>
#include <sys/wait.h>
#include <iostream>

CrashReportingLinux::CrashReportingLinux()
{
    _addressSpace = unw_create_addr_space(&_UPT_accessors, 0);
}

CrashReportingLinux::~CrashReportingLinux()
{
    unw_destroy_addr_space(_addressSpace);
}

std::vector<std::pair<uintptr_t, std::string>> CrashReportingLinux::GetThreadFrames(int32_t pid, int32_t tid, ResolveManagedMethod resolveManagedMethod)
{
    std::cout << "-------------- Inspecting thread " << tid << "\n";

    std::vector<std::pair<uintptr_t, std::string>> frames;

    if (ptrace(PTRACE_ATTACH, tid, NULL, NULL) == -1)
    {
        std::cout << "Ptrace failed for thread " << tid << "\n";
        return frames;
    }

    // Wait for the target process to stop
    int wait_status;
    waitpid(tid, &wait_status, 0);
    if (!WIFSTOPPED(wait_status))
    {
        fprintf(stderr, "Failed to stop target process\n");
        return frames;
    }

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

        std::cout << " - " << std::hex << ip << "\n";

        char symbol[1];

        int requiredBufferSize;

        if (resolveManagedMethod(ip, symbol, sizeof(symbol), &requiredBufferSize) == -1)
        {
            char* buffer = new char[requiredBufferSize];
            resolveManagedMethod(ip, buffer, requiredBufferSize, &requiredBufferSize);
            std::cout << buffer << "\n";
            delete[] buffer;
        }

        // unw_word_t offset;
        // unw_get_proc_name(&cursor, symbol, sizeof(symbol), &offset);
        std::cout << symbol << "\n";
    } while (unw_step(&cursor) > 0);

    if (ptrace(PTRACE_DETACH, tid, NULL, NULL) == -1) {
        perror("ptrace detach");
        return frames;
    }

    return frames;
}

std::vector<int32_t> CrashReportingLinux::GetThreads(int32_t pid)
{
    DIR* proc_dir;
    char dirname[256]; // Ensure sufficient space

    std::string pidPath = (pid == -1) ? "self" : std::to_string(pid);
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