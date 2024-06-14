#include "process_helper.h"
#include "cor_profiler.h"
#include "log.h"
#include "util.h"
#ifndef _WIN32
#include <fcntl.h>
#include <unistd.h>
#include <signal.h>
#include <pthread.h>
#include <sys/types.h>
#include <sys/wait.h>

extern char **environ;
#endif


using namespace shared;

namespace datadog::shared::nativeloader
{
/**
 * Start a process, send the input, and wait for it to exit.
 * @param processPath The path of the process to execute
 * @param args The additional arguments (if any) to send
 * @param The data to send through stdin
 * @return true if the process was invoked successfully, false otherwise
 */
bool ProcessHelper::RunProcess(const std::string& processPath,
                               const std::vector<std::string>& args,
                               std::string input)
{
#if _WIN32
    // For windows we combine the processPath and args into a single, space separated, string
    // and pass null for the application name
    // We assume that all the required escaping has been done etc

    std::string combined = processPath;
    for(const auto &arg: args)
    {
        combined += " " + arg;
    }

    auto commandLine = ToWSTRING(combined);

    SECURITY_ATTRIBUTES sa;

    sa.nLength = sizeof(SECURITY_ATTRIBUTES);
    sa.bInheritHandle = FALSE;
    sa.lpSecurityDescriptor = NULL;

    HANDLE hStdinRead, hStdinWrite;

    // Create a pipe for the child process's STDIN
    if (!CreatePipe(&hStdinRead, &hStdinWrite, &sa, 0))
    {
        Log::Warn("ProcessHelper::RunProcess: Failed to initialize the pipe");
        return false;
    }

    // Ensure the write handle to the pipe for STDIN is not inherited.
    if (!SetHandleInformation(hStdinRead, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT)) {
        Log::Warn("ProcessHelper::RunProcess: Failed to set handle information");
        CloseHandle(hStdinRead);
        CloseHandle(hStdinWrite);
        return false;
    }

    STARTUPINFO si;
    SecureZeroMemory(&si, sizeof(STARTUPINFO));
    si.cb = sizeof(STARTUPINFO);

    si.hStdError = GetStdHandle(STD_ERROR_HANDLE);
    si.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
    si.hStdInput = hStdinRead;
    si.dwFlags |= STARTF_USESTDHANDLES;

    // as it's not REQUIRED right now (the child inherits the caller's env vars),
    // and the docs show that it's a PITA:
    //
    // Each process has an environment block associated with it.
    // The environment block consists of a null-terminated block of null-terminated strings
    // (meaning there are two null bytes at the end of the block), where each string is in the form:
    // name=value
    // All strings in the environment block must be sorted alphabetically by name.
    // The sort is case-insensitive, Unicode order, without regard to locale.
    // Because the equal sign is a separator, it must not be used in the name of an environment variable.

    PROCESS_INFORMATION pi;
    if (!CreateProcess(
        nullptr,                  // lpApplicationName
        commandLine.data(),       // lpCommandLine
        nullptr,                  // lpProcessAttributes
        nullptr,                  // lpThreadAttributes
        TRUE,                     // bInheritHandles
        CREATE_NEW_PROCESS_GROUP, // dwCreationFlags (don't create as a child)
        nullptr,                  // lpEnvironment
        nullptr,                  // lpCurrentDirectory
        &si,                      // lpStartupInfo
        &pi))                     // lpProcessInformation
    {
        Log::Warn("ProcessHelper::RunProcess: Error starting ", processPath);
        CloseHandle(hStdinRead);
        CloseHandle(hStdinWrite);
        return false;
    }    

    DWORD written;
    WriteFile(hStdinWrite, input.c_str(), input.length(), &written, nullptr);

    CloseHandle(hStdinRead);
    CloseHandle(hStdinWrite);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    return true;
}
#else
    // The args include the path as the first argument
    std::vector<const char*> argv = {processPath.c_str()};
    for (const auto& arg : args)
    {
        argv.push_back(arg.c_str());
    }
    argv.push_back(nullptr);

    int pipefd[2];
    if (pipe(pipefd) == -1)
    {
        Log::Warn("ProcessHelper::RunProcess: Failed to initialize the pipe");
        return false;
    }

    int read_fd = pipefd[0];
    int write_fd = pipefd[1];

    pid_t processId = fork();

    if (processId == 0)
    {
        // Child process

        close(write_fd); // The child process doesn't need to write

        // Redirect stdin to the read end of the pipe
        if (dup2(read_fd, STDIN_FILENO) == -1)
        {
            _exit(EXIT_FAILURE);
        }

        close (read_fd); // Close the original read end of the pipe

        // Spawn the executable
        int error = execve(processPath.c_str(), const_cast<char* const *>(argv.data()), environ);

        // execve should take over the execution.
        // If we get here, it means execve failed, so we tear down the process.
        _exit(error != 0 ? error : EXIT_FAILURE);
    }

    // This part is only executed by the parent process

    if (processId > 0)
    {
        close(read_fd); // The parent process doesn't need to read

        write(write_fd, input.data(), input.length());
        close(write_fd);
    }
    else
    {
        Log::Warn("ProcessHelper::RunProcess: Error starting ", processPath);
        return false;
    }

    // .NET sets a handler for the SIGCHLD signal and ignores the processes it didn't start,
    // so we have to call waitpid to avoid creating a zombie process
    int status;
    waitpid(processId, &status, 0);

    return true;
}

#endif
}