#pragma once

#include <string>
#include <vector>
#ifndef _WIN32
#include <signal.h>
#include <sys/types.h>
#endif

namespace datadog::shared::nativeloader
{
#ifndef _WIN32    
typedef void (*VoidIntFn)(int);

enum
{
    READ_END_OF_PIPE = 0,
    WRITE_END_OF_PIPE = 1,
};
#endif

class ProcessHelper
{
private:
#ifndef _WIN32    
    static VoidIntFn handler_from_sigaction (struct sigaction *sa);
    static void CloseIfOpen(int fd);
    static inline bool CheckInterrupted(ssize_t result);
    static ssize_t WriteSize(int fd, const void* buffer, size_t count);
    static ssize_t ReadSize(int fd, void* buffer, size_t count);
    static void ExitChild(int pipeToParent, int error);
#endif
public:
    static bool RunProcess(const std::string& processPath, const std::vector<std::string>& args, std::string input);
};

} // namespace datadog::shared::nativeloader