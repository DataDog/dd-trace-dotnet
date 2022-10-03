#define _GNU_SOURCE
#include <dlfcn.h>
#include <link.h>
#include <signal.h>
#include <stddef.h>
#include <stdio.h>

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
We will rely on LD_PRELOAD mechanism to inject our own implementation of dl_iterate_phdr.
Before calling the real dl_iterate_phdr, we block all signals that would intefere with the thread.
Then will call the real dl_iterate_phdr.
When finished, we put back the previous block signals.

This is done by libunwind, just taking advantage of it.

*/

/* Function pointers to hold the value of the glibc functions */
static int (*__real_dl_iterate_phdr)(int (*callback)(struct dl_phdr_info* info, size_t size, void* data), void* data) = NULL;

int dl_iterate_phdr(int (*callback)(struct dl_phdr_info* info, size_t size, void* data), void* data)
{
    if (__real_dl_iterate_phdr == NULL)
    {
        __real_dl_iterate_phdr = dlsym(RTLD_NEXT, "dl_iterate_phdr");
    }

    sigset_t oldOne;
    sigset_t newOne;

    // initialize the set to all signals
    sigfillset(&newOne);

    // prevent any signals from interrupting the execution of the real dl_iterate_phdr
    pthread_sigmask(SIG_SETMASK, &newOne, &oldOne);

    // call the real dl_iterate_phdr (libc)
    int result = __real_dl_iterate_phdr(callback, data);

    // restore the previous state for signals
    pthread_sigmask(SIG_SETMASK, &oldOne, NULL);

    return result;
}

/*
 * dlopen, dladdr issue happens mainly on Alpine
 */

/* Function pointers to hold the value of the glibc functions */
static void* (*__real_dlopen)(const char* file, int mode) = NULL;

void* dlopen(const char* file, int mode)
{
    if (__real_dlopen == NULL)
    {
        __real_dlopen = dlsym(RTLD_NEXT, "dlopen");
    }

    sigset_t oldOne;
    sigset_t newOne;

    // initialize the set to all signals
    sigfillset(&newOne);

    // prevent any signals from interrupting the execution of the real dlopen
    pthread_sigmask(SIG_SETMASK, &newOne, &oldOne);

    // call the real dlopen (libc/musl-libc)
    void* result = __real_dlopen(file, mode);

    // restore the previous state for signals
    pthread_sigmask(SIG_SETMASK, &oldOne, NULL);

    return result;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_dladdr)(const void* addr_arg, Dl_info* info) = NULL;

int dladdr(const void* addr_arg, Dl_info* info)
{
    if (__real_dladdr == NULL)
    {
        __real_dladdr = dlsym(RTLD_NEXT, "dladdr");
    }

    sigset_t oldOne;
    sigset_t newOne;

    // initialize the set to all signals
    sigfillset(&newOne);

    // prevent any signals from interrupting the execution of the real dladdr
    pthread_sigmask(SIG_SETMASK, &newOne, &oldOne);

    // call the real dladdr (libc/musl-libc)
    int result = __real_dladdr(addr_arg, info);

    // restore the previous state for signals
    pthread_sigmask(SIG_SETMASK, &oldOne, NULL);

    return result;
}

#define INHIBIT_SIGNALS_FOR(fn_name)                                            \
    /* Function pointers to hold the value of the musl libc functions */            \
    static void (*__real_##fn_name)() = NULL;                                       \
                                                                                    \
    void fn_name()                                                                  \
    {                                                                               \
        if (__real_##fn_name == NULL)                                               \
        {                                                                           \
            __real_##fn_name = dlsym(RTLD_NEXT, #fn_name);                          \
        }                                                                           \
                                                                                    \
        sigset_t oldOne;                                                            \
        sigset_t newOne;                                                            \
                                                                                    \
        /* initialize the set to all signals*/                                      \
        sigfillset(&newOne);                                                        \
                                                                                    \
        /* prevent any signals from interrupting the execution of the real dladdr*/ \
        pthread_sigmask(SIG_SETMASK, &newOne, &oldOne);                             \
                                                                                    \
        /* call the real __inhibit_ptc (musl-libc) */                               \
        __real_##fn_name();                                                         \
                                                                                    \
        /* restore the previous state for signals*/                                 \
        pthread_sigmask(SIG_SETMASK, &oldOne, NULL);                                \
    }

INHIBIT_SIGNALS_FOR(__inhibit_ptc)
INHIBIT_SIGNALS_FOR(__acquire_ptc)
INHIBIT_SIGNALS_FOR(__release_ptc)