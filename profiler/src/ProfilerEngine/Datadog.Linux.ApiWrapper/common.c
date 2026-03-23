#define _GNU_SOURCE

#include "common.h"

#include <dlfcn.h>
#include <errno.h>
#include <pthread.h>
#include <stdio.h>
#include <unistd.h>

int (*volatile dd_set_shared_memory)(volatile int*) = NULL;

__attribute__((visibility("hidden"))) inline int __dd_set_shared_memory(volatile int* mem)
{
    // make a copy to avoid race
    int (*volatile set_shared_memory)(volatile int*) = dd_set_shared_memory;

    if (set_shared_memory == NULL)
        return 0;
    return set_shared_memory(mem);
}

void (*volatile dd_notify_libraries_cache_update)() = NULL;
__attribute__((visibility("hidden"))) inline void __dd_notify_libraries_cache_update()
{
    void (*volatile notify_libraries_cache_update)() = dd_notify_libraries_cache_update;

    if (notify_libraries_cache_update == NULL)
        return;

    notify_libraries_cache_update();
}

__attribute__((visibility("hidden"))) inline int is_interrupted_by_profiler(int rc, int error_code, int interrupted_by_profiler)
{
    return rc == -1L && error_code == EINTR && interrupted_by_profiler != 0;
}


void (*volatile dd_on_thread_routine_finished)() = NULL;
__attribute__((visibility("hidden"))) inline void __dd_on_thread_routine_finished()
{
    void (*volatile on_thread_routine_finished)() = dd_on_thread_routine_finished;

    if (on_thread_routine_finished == NULL)
        return;

    on_thread_routine_finished();
}

char* dlerror(void) __attribute__((weak));
static void* s_libdl_handle = NULL;
static void* s_libpthread_handle = NULL;
void* dlsym(void* handle, const char* symbol) __attribute__((weak));
int pthread_once(pthread_once_t* control, void (*init)(void)) __attribute__((weak));

static __typeof(dlopen)* s_dlopen = NULL;
void* __libc_dlopen_mode(const char* filename, int flag) __attribute__((weak));
void* __libc_dlsym(void* handle, const char* symbol) __attribute__((weak));

static void* __dd_dlopen(const char* filename, int flags)
{
    // it can be NULL: the first time we enter the function
    // or if libdl is not loaded yet
    if (!dlsym)
    {
        // if libdl.so is not loaded, use __libc_dlopen_mode
        s_dlopen = __libc_dlopen_mode;
    }
    else
    {
        // since we wrap dlopen, we want to call the real dlopen
        s_dlopen = dlsym(RTLD_NEXT, "dlopen");
    }
    if (s_dlopen)
    {
        void* ret = s_dlopen(filename, flags);
        return ret;
    }
    // Should not happen
    return NULL;
}

static void ensure_libdl_is_loaded()
{
    if (!dlsym && !s_libdl_handle)
    {
        s_libdl_handle = __dd_dlopen("libdl.so.2", RTLD_GLOBAL | RTLD_NOW);
    }
}

static void ensure_libpthread_is_loaded()
{
    if (!pthread_once && !s_libpthread_handle)
    {
        s_libpthread_handle = __dd_dlopen("libpthread.so.0", RTLD_GLOBAL | RTLD_NOW);
    }
}

__attribute__((visibility("hidden"))) int __dd_pthread_once(pthread_once_t* control, void (*init)(void))
{
    static __typeof(pthread_once)* pthread_once_ptr = &pthread_once;
    if (!pthread_once_ptr)
    {
        // pthread_once is not available: meaning libpthread.so was not
        // loaded at startup

        // First ensure that libpthread.so is loaded
        if (!s_libpthread_handle)
        {
            ensure_libpthread_is_loaded();
        }

        if (s_libpthread_handle)
        {
            pthread_once_ptr = __dd_dlsym(s_libpthread_handle, "pthread_once");
        }

        // Should not happen
        if (!pthread_once_ptr)
        {
            return -1;
        }
    }

    return pthread_once_ptr(control, init);
}

__attribute__((visibility("hidden"))) void* __dd_dlsym(void* handle, const char* symbol)
{
    static __typeof(dlsym)* dlsym_ptr = &dlsym;
    if (!dlsym_ptr)
    {
        // dlsym is not available: meaning we are on glibc and libdl.so was not
        // loaded at startup

        // First ensure that libdl.so is loaded
        if (!s_libdl_handle)
        {
            ensure_libdl_is_loaded();
        }

        if (s_libdl_handle)
        {
            // locate dlsym in libdl.so by using internal libc.so.6 function
            // __libc_dlsym.
            // Note that we need dlsym because __libc_dlsym does not provide
            // RTLD_DEFAULT/RTLD_NEXT functionality.
            dlsym_ptr = __libc_dlsym(s_libdl_handle, "dlsym");
        }

        // Should not happen
        if (!dlsym_ptr)
        {
            return NULL;
        }
    }

    return dlsym_ptr(handle, symbol);
}