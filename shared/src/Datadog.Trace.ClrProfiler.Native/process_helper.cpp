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
 * Start a process, in a "fire and forget" way, sending arguments.
 * No attempt is made to connect to the process, establish a pipe, or anything else.
 * @param processPath The path of the process to execute
 * @param args The additional arguments (if any) to send
 * @return true if the process was invoked successfully, false otherwise
 */
bool ProcessHelper::RunProcess(const std::string& processPath,
                               const std::vector<std::string>& args)
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

    STARTUPINFO si;
    SecureZeroMemory(&si, sizeof(STARTUPINFO));
    si.cb = sizeof(STARTUPINFO);

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
    if (CreateProcess(
        nullptr,                  // lpApplicationName
        commandLine.data(),       // lpCommandLine
        nullptr,                  // lpProcessAttributes
        nullptr,                  // lpThreadAttributes
        FALSE,                    // bInheritHandles
        CREATE_NEW_PROCESS_GROUP, // dwCreationFlags (don't create as a child)
        nullptr,                  // lpEnvironment
        nullptr,                  // lpCurrentDirectory
        &si,                      // lpStartupInfo
        &pi))                     // lpProcessInformation
    {
        // we don't need to wait for it to finish, so just free-up resources 
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        return true;
    }

    // Error starting
    return false;
}
#else
    // The args include the path as the first argument
    std::vector<const char*> argv = {processPath.c_str()};
    for (const auto& arg : args)
    {
        argv.push_back(arg.c_str());
    }
    argv.push_back(nullptr);

    // Largely blindly copied from the .NET runtime code
    // and then deleted stuff I didn't _think_ we needed for this basic scenario :blindfold:
    // https://github.com/dotnet/runtime/blob/f4c9264fe8448fdf1f66eda04a582cbade40cd39/src/native/libs/System.Native/pal_process.c#L212

    bool success = true;
    int waitForChildToExecPipe[2] = {-1, -1};
    pid_t processId = -1;
    sigset_t signal_set;
    sigset_t old_signal_set;

    // The fork child must not be signalled until it calls exec(): our signal handlers do not
    // handle being raised in the child process correctly
    sigfillset(&signal_set);
    pthread_sigmask(SIG_SETMASK, &signal_set, &old_signal_set);

    if ((processId = fork()) == 0) // processId == 0 if this is child process
    {

        // It turns out that child processes depend on their sigmask being set to something sane rather than mask all.
        // On the other hand, we have to mask all to avoid our own signal handlers running in the child process, writing
        // to the pipe, and waking up the handling thread in the parent process. This also avoids third-party code getting
        // equally confused.
        // Remove all signals, then restore signal mask.
        // Since we are in a vfork() child, the only safe signal values are SIG_DFL and SIG_IGN.  See man 3 libthr on BSD.
        // "The implementation interposes the user-installed signal(3) handlers....to postpone signal delivery to threads
        // which entered (libthr-internal) critical sections..."  We want to pass SIG_DFL anyway.
        sigset_t junk_signal_set;
        struct sigaction sa_default;
        struct sigaction sa_old;
        memset(&sa_default, 0, sizeof(sa_default)); // On some architectures, sa_mask is a struct so assigning zero to it doesn't compile
        sa_default.sa_handler = SIG_DFL;
        for (int sig = 1; sig < NSIG; ++sig)
        {

            if (sig == SIGKILL || sig == SIGSTOP)
            {
                continue;
            }
            if (!sigaction(sig, NULL, &sa_old))
            {
                void (*oldhandler)(int) = handler_from_sigaction (&sa_old);

                if (oldhandler != SIG_IGN && oldhandler != SIG_DFL)
                {
                    // It has a custom handler, put the default handler back.
                    // We check first to preserve flags on default handlers.
                    sigaction(sig, &sa_default, NULL);
                }
            }
        }

        pthread_sigmask(SIG_SETMASK, &old_signal_set, &junk_signal_set); // Not all architectures allow NULL here

        // Finally, execute the new process.  execve will not return if it's successful.
        execve(processPath.c_str(), const_cast<char* const *>(argv.data()), environ);
        ExitChild(waitForChildToExecPipe[WRITE_END_OF_PIPE], errno); // execve failed
    }

    // Restore signal mask in the parent process immediately after fork() or vfork() call

    pthread_sigmask(SIG_SETMASK, &old_signal_set, &signal_set);

    if (processId < 0)
    {

        // failed
        success = false;
    }

    // Also close the write end of the exec waiting pipe, and wait for the pipe to be closed
    // by trying to read from it (the read will wake up when the pipe is closed and broken).
    // Ignore any errors... this is a best-effort attempt.

    CloseIfOpen(waitForChildToExecPipe[WRITE_END_OF_PIPE]);
    if (waitForChildToExecPipe[READ_END_OF_PIPE] != -1)
    {

        int childError;
        if (success)
        {

            ssize_t result = ReadSize(waitForChildToExecPipe[READ_END_OF_PIPE], &childError, sizeof(childError));
            if (result == sizeof(childError))
            {
                success = false;
            }
        }
        CloseIfOpen(waitForChildToExecPipe[READ_END_OF_PIPE]);
    }

    // If we failed, close everything else and give back error values in all out arguments.
    if (!success)
    {
        Log::Warn("SingleStepGuardRails::SendTelemetry: Error calling telemetry forwarder");
        // Reap child
        if (processId > 0)
        {
            int status;
            waitpid(processId, &status, 0);
        }
    }

    return success;
}

VoidIntFn ProcessHelper::handler_from_sigaction (struct sigaction *sa)
{
    if (((unsigned int)sa->sa_flags) & SA_SIGINFO)
    {
        // work around -Wcast-function-type
        void (*tmp)(void) = (void (*)(void))sa->sa_sigaction;
        return (void (*)(int))tmp;
    }
    else
    {
        return sa->sa_handler;
    }
}

void ProcessHelper::CloseIfOpen(int fd)
{
    if (fd >= 0)
    {
        close(fd); // Ignoring errors from close is a deliberate choice
    }
}

inline bool ProcessHelper::CheckInterrupted(ssize_t result)
{
    return result < 0 && errno == EINTR;
}

ssize_t ProcessHelper::WriteSize(int fd, const void* buffer, size_t count)
{
    ssize_t rv = 0;
    while (count > 0)
    {
        ssize_t result = 0;
        while (CheckInterrupted(result = write(fd, buffer, count)));
        if (result > 0)
        {
            rv += result;
            buffer = (const uint8_t*)buffer + result;
            count -= (size_t)result;
        }
        else
        {
            return -1;
        }
    }
    return rv;
}

ssize_t ProcessHelper::ReadSize(int fd, void* buffer, size_t count)
{
    ssize_t rv = 0;
    while (count > 0)
    {
        ssize_t result = 0;
        while (CheckInterrupted(result = read(fd, buffer, count)));
        if (result > 0)
        {
            rv += result;
            buffer = (uint8_t*)buffer + result;
            count -= (size_t)result;
        }
        else
        {
            return -1;
        }
    }
    return rv;
}

void ProcessHelper::ExitChild(int pipeToParent, int error)
{
    if (pipeToParent != -1)
    {
        WriteSize(pipeToParent, &error, sizeof(error));
    }
    _exit(error != 0 ? error : EXIT_FAILURE);
}

#endif
}