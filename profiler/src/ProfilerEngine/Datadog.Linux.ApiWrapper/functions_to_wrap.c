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

// this function is called by the profiler
unsigned long long dd_inside_wrapped_functions()
{
    return functions_entered_counter;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_dl_iterate_phdr)(int (*callback)(struct dl_phdr_info* info, size_t size, void* data), void* data) = NULL;

int dl_iterate_phdr(int (*callback)(struct dl_phdr_info* info, size_t size, void* data), void* data)
{
    if (__real_dl_iterate_phdr == NULL)
    {
        __real_dl_iterate_phdr = dlsym(RTLD_NEXT, "dl_iterate_phdr");
    }

    ((char*)&functions_entered_counter)[ENTERED_DL_ITERATE_PHDR]++;

    // call the real dl_iterate_phdr (libc)
    int result = __real_dl_iterate_phdr(callback, data);

    ((char*)&functions_entered_counter)[ENTERED_DL_ITERATE_PHDR]--;

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

    ((char*)&functions_entered_counter)[ENTERED_DL_OPEN]++;

    // call the real dlopen (libc/musl-libc)
    void* result = __real_dlopen(file, mode);

    ((char*)&functions_entered_counter)[ENTERED_DL_OPEN]--;

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

    ((char*)&functions_entered_counter)[ENTERED_DL_ADDR]++;

    // call the real dladdr (libc/musl-libc)
    int result = __real_dladdr(addr_arg, info);

    ((char*)&functions_entered_counter)[ENTERED_DL_ADDR]--;

    return result;
}

#ifdef DD_ALPINE

/* Function pointers to hold the value of the glibc functions */
static int (*__real_pthread_create)(pthread_t* restrict res, const pthread_attr_t* restrict attrp, void* (*entry)(void*), void* restrict arg) = NULL;

int pthread_create(pthread_t* restrict res, const pthread_attr_t* restrict attrp, void* (*entry)(void*), void* restrict arg)
{
    if (__real_pthread_create == NULL)
    {
        __real_pthread_create = dlsym(RTLD_NEXT, "pthread_create");
    }

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
    if (__real_pthread_attr_init == NULL)
    {
        __real_pthread_attr_init = dlsym(RTLD_NEXT, "pthread_attr_init");
    }

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
    if (__real_pthread_getattr_default_np == NULL)
    {
        __real_pthread_getattr_default_np = dlsym(RTLD_NEXT, "pthread_getattr_default_np");
    }

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
    if (__real_pthread_setattr_default_np == NULL)
    {
        __real_pthread_setattr_default_np = dlsym(RTLD_NEXT, "pthread_setattr_default_np");
    }

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_SETATTR_DEFAULT_NP]++;

    // call the real pthread_setattr_default_np (libc/musl-libc)
    int result = __real_pthread_setattr_default_np(a);

    ((char*)&functions_entered_counter)[ENTERED_PTHREAD_SETATTR_DEFAULT_NP]--;

    return result;
}

/* Function pointers to hold the value of the glibc functions */
static int (*__real_fork)() = NULL;

pid_t fork()
{
    if (__real_fork == NULL)
    {
        __real_fork = dlsym(RTLD_NEXT, "fork");
    }

    ((char*)&functions_entered_counter)[ENTERED_FORK]++;

    // call the real fork (libc/musl-libc)
    pid_t result = __real_fork();

    ((char*)&functions_entered_counter)[ENTERED_FORK]--;

    return result;
}

#endif

#if DD_UNIVERSAL_BINARY
char* dlerror(void) __attribute__((weak));

static __typeof(dlerror)* s_dlerror = &dlerror;
static __typeof(dlopen)* s_dlopen = &dlopen;
int pthread_cancel(pthread_t thread) __attribute__((weak));

double log(double x) __attribute__((weak));

// NOLINTNEXTLINE cert-dcl51-cpp
void* __libc_dlopen_mode(const char* filename, int flag) __attribute__((weak));

static void* my_dlopen(const char* filename, int flags)
{
    if (!s_dlopen)
    {
        // if libdl.so is not loaded, use __libc_dlopen_mode
        s_dlopen = __libc_dlopen_mode;
    }
    if (s_dlopen)
    {
        void* ret = s_dlopen(filename, flags);
        if (!ret && s_dlerror)
        {
            fprintf(stderr, "Failed to dlopen %s (%s)\n", filename, s_dlerror());
        }
        return ret;
    }
    // Should not happen
    return NULL;
}

static void ensure_libm_is_loaded()
{
    if (!log)
    {
        my_dlopen("libm.so.6", RTLD_GLOBAL | RTLD_NOW);
    }
}

static void ensure_libpthread_is_loaded()
{
    if (!pthread_cancel)
    {
        my_dlopen("libpthread.so.0", RTLD_GLOBAL | RTLD_NOW);
    }
}

static void __attribute__((constructor)) loader()
{
    ensure_libm_is_loaded();
    ensure_libpthread_is_loaded();
}
#endif