#define _GNU_SOURCE
#include <dlfcn.h>
#include <link.h>
#include <signal.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <unistd.h>
#include <stdatomic.h>
#include <pthread.h>

#include "common.h"

/* dl_iterate_phdr wrapper
The .NET profiler on Linux uses a classic signal-based approach to collect thread callstack.
Which means that we send a signal (USR1 or USR2) to the thread we want to collect. When the thread handles the signal
it calls the signal handler and the thread will start to walk its callstack using libunwind.
In order to correctly unwind the callstack, libunwind locates the symbol associated to an instruction pointer by calling dl_iterate_phdr.
dl_iterated_phdr will go over the loaded shared objects and execute the callback libunwind provided.

When an exception is thrown, the CLR call libc/libgcc functions to unwind the callstack. Same as libunwind, to
correctly unwind the stack it will call dl_iterate_phdr.

Before going through the list of shared object, dl_iterate_phdr will acquire a lock to avoid modification of that list.

The deadlock:
An exception is thrown during an application thread execution. The CLR unwinds the callstack and a call to dl_iterate_phdr is made.
The lock is acquired. But at the same time, a signal is sent to the same thread and its execution is hijacked by the profiler.
The hijacked thread will start walking its callstack and a call to dl_iterate_phdr will be made. But it seems that the lock in
dl_iterate_phdr is not recursive and the thread is blocked.

%%%%%%%%%%%%%%%%%%%%%%%%%%% Fix
One way is to use the LD_PRELOAD trick:
We will rely on LD_PRELOAD mechanism to inject our own implementation of dl_iterate_phdr.
Before calling the real dl_iterate_phdr, we block all signals that would intefere with the thread.
Then will call the real dl_iterate_phdr.
When finished, we put back the previous block signals. (This is done by libunwind, just taking advantage of it.)

But this has a non-negligible overhead. Instead, we will incr/decr a counter per function
each time the thread enters/exits from it.
The profiler will just have to check if this counter is equal to 0 to profiler or not.

*/

enum FUNCTION_ID
{
    ENTERED_DL_ITERATE_PHDR = 0,
    ENTERED_DL_OPEN = 1,
    ENTERED_DL_ADDR = 2,
    ENTERED_PTHREAD_CREATE = 3,
    ENTERED_PTHREAD_ATTR_INIT = 4,
    ENTERED_PTHREAD_GETATTR_DEFAULT_NP = 5,
    ENTERED_PTHREAD_SETATTR_DEFAULT_NP = 6,
    ENTERED_FORK = 7
};

// counters: one byte per function
__thread unsigned long long functions_entered_counter = 0;

__attribute__((visibility("hidden")))
atomic_int is_app_crashing = 0;

// this function is called by the profiler
unsigned long long dd_inside_wrapped_functions()
{
    return functions_entered_counter + is_app_crashing;
}

#if defined(__aarch64__)
const char* DdDotnetFolder = "linux-arm64";
const char* DdDotnetMuslFolder = "linux-musl-arm64";
#else
const char* DdDotnetFolder = "linux-x64";
const char* DdDotnetMuslFolder = "linux-musl-x64";
#endif

char* crashHandler = NULL;

const char* getLibraryPath()
{
    Dl_info dl_info;

    if (dladdr((void*)getLibraryPath, &dl_info))
    {
        return dl_info.dli_fname;
    }
    
    return NULL;
}

int isAlpine()
{
    if (access("/etc/alpine-release", F_OK) == 0)
    {
        return 1;
    }

    return 0;
}

char* appendToPath(const char* folder, const char* suffix)
{
    int length = strlen(folder) + strlen(suffix);

    char* result = malloc(length + 1);

    if (result == NULL)
    {
        return NULL;
    }

    strcpy(result, folder);
    strcpy(result + strlen(folder), suffix);

    return result;
}

char* getFolder(const char* path)
{
    char* lastSlash = strrchr(path, '/');
    if (lastSlash == NULL)
    {
        return "";
    }

    // Copying the last subfolder to a new string
    int length = lastSlash - path;
    char* folder = malloc(length + 1);

    if (folder == NULL)
    {
        return NULL; // Memory allocation failed
    }

    strncpy(folder, path, length);
    folder[length] = '\0';

    return folder;
}

char* getSubfolder(const char* path)
{
    char* lastSlash = strrchr(path, '/');
    if (lastSlash == NULL) {
        return NULL; // No slash found, path does not contain subfolders
    }

    // Finding the last but one slash
    char* temp = lastSlash - 1;
    while (temp >= path && *temp != '/') {
        temp--;
    }

    if (temp < path)
    {
        // No leading slash, that's weird
        return NULL;
    }

    // Increment to move past the slash
    temp++;

    // Copying the last subfolder to a new string
    int folderLength = lastSlash - temp;
    char* subfolder = malloc(folderLength + 1);

    if (subfolder == NULL)
    {
        return NULL; // Memory allocation failed
    }

    strncpy(subfolder, temp, folderLength);
    subfolder[folderLength] = '\0';

    return subfolder;
}


static void check_init();

static char* originalMiniDumpName = NULL;
static const char* datadogCrashMarker = "datadog_crashtracking";
#define DD_CRASHTRACKING_ENABLED "DD_CRASHTRACKING_ENABLED"
#define DD_INTERNAL_CRASHTRACKING_PASSTHROUGH "DD_INTERNAL_CRASHTRACKING_PASSTHROUGH"
#define DOTNET_DbgEnableMiniDump "DOTNET_DbgEnableMiniDump"
#define COMPlus_DbgEnableMiniDump "COMPlus_DbgEnableMiniDump"
#define DOTNET_DbgMiniDumpName "DOTNET_DbgMiniDumpName"
#define COMPlus_DbgMiniDumpName "COMPlus_DbgMiniDumpName"
#define DD_INTERNAL_CRASHTRACKING_MINIDUMPNAME "DD_INTERNAL_CRASHTRACKING_MINIDUMPNAME"

__attribute__((constructor))
void initLibrary(void)
{
    check_init();

    const char* crashHandlerEnabled = getenv(DD_CRASHTRACKING_ENABLED);

    if (crashHandlerEnabled != NULL)
    {
        if (strcasecmp(crashHandlerEnabled, "no") == 0
         || strcasecmp(crashHandlerEnabled, "false") == 0
         || strcasecmp(crashHandlerEnabled, "0") == 0)
        {
            // Early nope
            return;
        }
    }

    // Bash provides its own version of the getenv/setenv functions
    // Fetch the original ones and use those instead
    char *(*real_getenv)(const char *) = __dd_dlsym(RTLD_NEXT, "getenv");
    int (*real_setenv)(const char *, const char *, int) = __dd_dlsym(RTLD_NEXT, "setenv");

    if (real_getenv == NULL || real_setenv == NULL)
    {
        return;
    }

    // If crashtracking is enabled, check the value of DOTNET_DbgEnableMiniDump
    // If set, set DD_TRACE_CRASH_HANDLER_PASSTHROUGH to indicate dd-dotnet that it should call createdump
    // If not set, set it to 1 so that .NET calls createdump in case of crash
    // (and we will redirect the call to dd-dotnet)
    // The path to the crash handler is not set, try to deduce it  
    const char* libraryPath = getLibraryPath();

    if (libraryPath != NULL)
    {            
        // If the library is in linux-x64 or linux-musl-x64, we use that folder
        // Otherwise, if the library is in continuousprofiler, we have to call isAlpine()
        // and use either ../linux-x64/ or ../linux-musl-x64/, or their ARM64 equivalent
        char* subFolder = getSubfolder(libraryPath);

        if (subFolder != NULL)
        {
            char* newCrashHandler = NULL;

            if (strcmp(subFolder, DdDotnetFolder) == 0
                || strcmp(subFolder, DdDotnetMuslFolder) == 0)
            {
                // We use the dd-dotnet in that same folder
                char* folder = getFolder(libraryPath);

                if (folder != NULL)
                {
                    asprintf(&newCrashHandler, "%s/dd-dotnet", folder);
                    free(folder);
                }
            }
            else
            {
                char* folder = getFolder(libraryPath);

                if (folder != NULL)
                {
                    const char* currentDdDotnetFolder;

                    if (isAlpine() == 0)
                    {
                        currentDdDotnetFolder = DdDotnetFolder;
                    }
                    else
                    {
                        currentDdDotnetFolder = DdDotnetMuslFolder;
                    }

                    if (strcmp(subFolder, "continuousprofiler") == 0)
                    {
                        // If we're in continuousprofiler, we need to go up one folder
                        asprintf(&newCrashHandler, "%s/../%s/dd-dotnet", folder, currentDdDotnetFolder);
                    }
                    else
                    {
                        // Assume we're at the root
                        asprintf(&newCrashHandler, "%s/%s/dd-dotnet", folder, currentDdDotnetFolder);
                    }
                        
                    free(folder);
                }
            }

            free(subFolder);

            if (newCrashHandler != NULL)
            {
                // Make sure the file exists and has execute permissions
                if (access(newCrashHandler, X_OK) == 0)
                {
                    crashHandler = newCrashHandler;
                }
                else
                {
                    free(newCrashHandler);
                }
            }
        }
    }

    if (crashHandler != NULL && crashHandler[0] != '\0')
    {
        char* enableMiniDump = real_getenv(DOTNET_DbgEnableMiniDump);

        if (enableMiniDump == NULL)
        {
            enableMiniDump = real_getenv(COMPlus_DbgEnableMiniDump);
        }

        if (enableMiniDump != NULL && enableMiniDump[0] == '1')
        {
            // Passthrough is expected by dd-dotnet to know whether it should forward the call to createdump
            char* passthrough = real_getenv(DD_INTERNAL_CRASHTRACKING_PASSTHROUGH);

            if (passthrough == NULL || passthrough[0] == '\0')
            {
                // If passthrough is already set, don't override it
                // This handles the case when, for example, the user calls dotnet run
                //  - dotnet run sets DOTNET_DbgEnableMiniDump=1
                //  - dotnet then launches the target app
                //  - the target app thinks DOTNET_DbgEnableMiniDump has been set by the user and enables passthrough
                real_setenv(DD_INTERNAL_CRASHTRACKING_PASSTHROUGH, "1", 1);
            }
        }
        else
        {
            // If DOTNET_DbgEnableMiniDump is not set, we set it so that the crash handler is called,
            // but we instruct it to not call createdump afterwards
            real_setenv(COMPlus_DbgEnableMiniDump, "1", 1);
            real_setenv(DOTNET_DbgEnableMiniDump, "1", 1);
            real_setenv(DD_INTERNAL_CRASHTRACKING_PASSTHROUGH, "0", 1);
        }

        originalMiniDumpName = real_getenv(DOTNET_DbgMiniDumpName);

        if (originalMiniDumpName == NULL || strncmp(originalMiniDumpName, datadogCrashMarker, strlen(datadogCrashMarker)) == 0)
        {
            originalMiniDumpName = real_getenv(COMPlus_DbgMiniDumpName);
        }

        if (originalMiniDumpName != NULL && strncmp(originalMiniDumpName, datadogCrashMarker, strlen(datadogCrashMarker)) == 0)
        {
            // If LD_PRELOAD was set in the parent process, then we replaced COMPlus_DbgMiniDumpName with datadogCrashMarker and lost the original value
            // We use DD_INTERNAL_CRASHTRACKING_MINIDUMPNAME to retrieve it
            originalMiniDumpName = real_getenv(DD_INTERNAL_CRASHTRACKING_MINIDUMPNAME);
        }

        if (originalMiniDumpName != NULL && originalMiniDumpName[0] != '\0')
        {
            // Save the original value in DD_INTERNAL_CRASHTRACKING_MINIDUMPNAME so that child processes can retrieve it
            real_setenv(DD_INTERNAL_CRASHTRACKING_MINIDUMPNAME, originalMiniDumpName, 1);
        }

        real_setenv(COMPlus_DbgMiniDumpName, datadogCrashMarker, 1);
        real_setenv(DOTNET_DbgMiniDumpName, datadogCrashMarker, 1);
    }
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_dl_iterate_phdr)(int (*callback)(struct dl_phdr_info* info, size_t size, void* data), void* data) = NULL;

int dl_iterate_phdr(int (*callback)(struct dl_phdr_info* info, size_t size, void* data), void* data)
{
    check_init();

    ((char*)&functions_entered_counter)[ENTERED_DL_ITERATE_PHDR]++;

    // call the real dl_iterate_phdr (libc)
    int result = __real_dl_iterate_phdr(callback, data);

    ((char*)&functions_entered_counter)[ENTERED_DL_ITERATE_PHDR]--;

    return result;
}

/*
 * dlopen, dladdr issue happens mainly on Alpine
 */

__attribute__((visibility("hidden")))
atomic_ullong __dd_dlopen_dlcose_calls_counter = 0;

unsigned long long dd_nb_calls_to_dlopen_dlclose()
{
    return __dd_dlopen_dlcose_calls_counter;
}

/* Function pointers to hold the value of the glibc functions */
static void* (*__real_dlopen)(const char* file, int mode) = NULL;

void* dlopen(const char* file, int mode)
{
    check_init();

    ((char*)&functions_entered_counter)[ENTERED_DL_OPEN]++;

    // call the real dlopen (libc/musl-libc)
    void* result = __real_dlopen(file, mode);
    __dd_dlopen_dlcose_calls_counter++;

    ((char*)&functions_entered_counter)[ENTERED_DL_OPEN]--;

    return result;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_dlclose)(void* handle) = NULL;

int dlclose(void* handle)
{
    check_init();

    // call the real dlopen (libc/musl-libc)
    int result = __real_dlclose(handle);
    __dd_dlopen_dlcose_calls_counter++;

    return result;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_dladdr)(const void* addr_arg, Dl_info* info) = NULL;

int dladdr(const void* addr_arg, Dl_info* info)
{
    check_init();

    ((char*)&functions_entered_counter)[ENTERED_DL_ADDR]++;

    // call the real dladdr (libc/musl-libc)
    int result = __real_dladdr(addr_arg, info);

    ((char*)&functions_entered_counter)[ENTERED_DL_ADDR]--;

    return result;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_execve)(const char* pathname, char* const argv[], char* const envp[]) = NULL;

__attribute__((visibility("hidden")))
int ShouldCallCustomCreatedump(const char* pathname, char* const argv[])
{
    if (crashHandler == NULL || pathname == NULL)
    {
        return 0;
    }

    size_t length = strlen(pathname);

    if (length < 11 || strcmp(pathname + length - 11, "/createdump") != 0)
    {
        return 0;
    }

    // datadog_crashtracking is set to identify actual crash to dump generation requests (ex: dotnet-dump)
    int previousWasNameOpt = 0;
    for (int i = 0; argv[i] != NULL; i++)
    {
        if (previousWasNameOpt != 0 && strncmp(argv[i], datadogCrashMarker, strlen(datadogCrashMarker)) == 0)
        {
            return 1;
        }
        previousWasNameOpt = strncmp(argv[i], "--name", strlen("--name")) == 0;
    }

    return 0;
}

int execve(const char* pathname, char* const argv[], char* const envp[])
{
    check_init();

    int callCustomCreatedump = ShouldCallCustomCreatedump(pathname, argv);

    if (callCustomCreatedump == 0)
    {
        return __real_execve(pathname, argv, envp);
    }

    is_app_crashing = 1;
    // Execute the alternative crash handler, and prepend "createdump" to the arguments

    // Count the number of arguments (the list ends with a null pointer)
    int argc = 0;
    while (argv[argc++] != NULL);

    // We add two arguments: the path to dd-dotnet, and "createdump"
    char** newArgv = malloc((argc + 2) * sizeof(char*));

    // By convention, argv[0] contains the name of the executable
    // Insert createdump as the first actual argument
    newArgv[0] = crashHandler;
    newArgv[1] = "createdump";

    // Copy the remaining arguments and replace datadog_crashtracking by the original name if needed
    size_t idx = 0;
    size_t new_idx = 2;
    while (argv[idx] != NULL)
    {
        if (strncmp(argv[idx], "--name", strlen("--name")) == 0)
        {
            if (originalMiniDumpName != NULL)
            {
                newArgv[new_idx++] = "--name";
                newArgv[new_idx++] = originalMiniDumpName; // no need to check for datadog_crashtracking, this was done in ShouldCallOurOwnCreatedump
            }

            idx += 2;
        }
        else
        {
            newArgv[new_idx++] = argv[idx++];
        }
    }
    newArgv[new_idx] = NULL;  // NULL terminate the array

    size_t envp_count;
    for (envp_count = 0; envp[envp_count]; ++envp_count);
    char** new_envp = malloc((envp_count + 1) * sizeof(char*)); // +1 for NULL terminator

    int index = 0;

    for (size_t i = 0; i < envp_count; ++i) {
        if (strncmp(envp[i], "LD_PRELOAD=", strlen("LD_PRELOAD=")) == 0) {
            continue;
        }

        if (strncmp(envp[i], "CORECLR_ENABLE_PROFILING=", strlen("CORECLR_ENABLE_PROFILING=")) == 0) {
            continue;
        }

        if (strncmp(envp[i], "DOTNET_DbgEnableMiniDump=", strlen("DOTNET_DbgEnableMiniDump=")) == 0) {
            continue;
        }

        if (strncmp(envp[i], "COMPlus_DbgEnableMiniDump=", strlen("COMPlus_DbgEnableMiniDump=")) == 0) {
            continue;
        }

        new_envp[index++] = envp[i];
    }
    new_envp[index] = NULL; // NULL terminate the array

    int result = __real_execve(crashHandler, newArgv, new_envp);

    free(newArgv);
    free(new_envp);

    return result;
}

#ifdef DD_ALPINE

/* Function pointers to hold the value of the glibc functions */
static int (*__real_pthread_create)(pthread_t* restrict res, const pthread_attr_t* restrict attrp, void* (*entry)(void*), void* restrict arg) = NULL;

int pthread_create(pthread_t* restrict res, const pthread_attr_t* restrict attrp, void* (*entry)(void*), void* restrict arg)
{
    check_init();

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_CREATE]++;

    // call the real pthread_create (libc/musl-libc)
    int result = __real_pthread_create(res, attrp, entry, arg);

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_CREATE]--;

    return result;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_pthread_attr_init)(pthread_attr_t* a) = NULL;

int pthread_attr_init(pthread_attr_t* a)
{
    check_init();
    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_ATTR_INIT]++;

    // call the real pthread_attr_init (libc/musl-libc)
    int result = __real_pthread_attr_init(a);

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_ATTR_INIT]--;

    return result;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_pthread_getattr_default_np)(pthread_attr_t* attrp) = NULL;

int pthread_getattr_default_np(pthread_attr_t* a)
{
    check_init();

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_GETATTR_DEFAULT_NP]++;

    // call the real pthread_getattr_default_np (libc/musl-libc)
    int result = __real_pthread_getattr_default_np(a);

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_GETATTR_DEFAULT_NP]--;

    return result;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_pthread_setattr_default_np)(const pthread_attr_t* attrp) = NULL;

int pthread_setattr_default_np(const pthread_attr_t* a)
{
    check_init();

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_SETATTR_DEFAULT_NP]++;

    // call the real pthread_setattr_default_np (libc/musl-libc)
    int result = __real_pthread_setattr_default_np(a);

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_SETATTR_DEFAULT_NP]--;

    return result;
}

#if 0
// Remove the wrapping around fork because in Universal this cause deadlock on 
// debian stretch slim
// In debian stretch slim, it's impossible to install gdb and other tools to
// investigate the deadlock.
// Since this wrapping was done for safety but no actual issue, we remove it for now.
// But we leave the code for documentation or if we need to reactivate it.
/* Function pointers to hold the value of the glibc functions */
static int (*__real_fork)() = NULL;

pid_t fork()
{
    check_init();

    ((char*)&functions_entered_counter)[ENTERED_FORK]++;

    // call the real fork (libc/musl-libc)
    pid_t result = __real_fork();

    ((char*)&functions_entered_counter)[ENTERED_FORK]--;

    return result;
}
#endif
#endif
static pthread_once_t once_control = PTHREAD_ONCE_INIT;

static void init()
{
    __real_dl_iterate_phdr = __dd_dlsym(RTLD_NEXT, "dl_iterate_phdr");
    __real_dlopen = __dd_dlsym(RTLD_NEXT, "dlopen");
    __real_dlclose = __dd_dlsym(RTLD_NEXT, "dlclose");
    __real_dladdr = __dd_dlsym(RTLD_NEXT, "dladdr");
    __real_execve = __dd_dlsym(RTLD_NEXT, "execve");
#ifdef DD_ALPINE
    __real_pthread_create = __dd_dlsym(RTLD_NEXT, "pthread_create");
    __real_pthread_attr_init = __dd_dlsym(RTLD_NEXT, "pthread_attr_init");
    __real_pthread_getattr_default_np = __dd_dlsym(RTLD_NEXT, "pthread_getattr_default_np");
    __real_pthread_setattr_default_np = __dd_dlsym(RTLD_NEXT, "pthread_setattr_default_np");
    //__real_fork = __dd_dlsym(RTLD_NEXT, "fork");
#endif
}

static void check_init()
{
    __dd_pthread_once(&once_control, init);
}
