#define _GNU_SOURCE
#include <dlfcn.h>
#include <stdlib.h>
#include <threads.h>

#include "common.h"

/*
* We found recently a new deadlock where the thread to be sampled acquired the malloc' lock and is waiting on a lock in dl_iterate_phdr.
* It is waiting because, another thread took the same lock (by calling dlopen) and is waiting on the malloc' lock.
* We wrap the (de-)allocation functions to know if the thread, to be sampled, was requesting/releasing memory 
* (so acquired the malloc' lock) when interrupted by our signal.
*/

#define ALLOCATION_VISIBILITY __attribute__((visibility("hidden")))
ALLOCATION_VISIBILITY
__thread unsigned int allocation_api_usage_counter = 0;

ALLOCATION_VISIBILITY int dd_is_using_allocation_api()
{
    return allocation_api_usage_counter;
}

#define DECLARE_FN(return_type, name, parameters)           \
    ALLOCATION_VISIBILITY                                   \
    return_type temp_##name(END(PARAMS_LOOP_0 parameters)); \
    ALLOCATION_VISIBILITY                                   \
    return_type (*__dd_real_##name)(END(PARAMS_LOOP_0 parameters)) = &temp_##name;

DECLARE_FN(void*, calloc, (size_t, nmemb)(size_t, size))
DECLARE_FN(void*, malloc, (size_t, size))
DECLARE_FN(void*, aligned_alloc, (size_t, align)(size_t, size))
DECLARE_FN(void, free, (void*, ptr))
DECLARE_FN(void*, realloc, (void*, ptr)(size_t, size))
DECLARE_FN(void*, reallocarray, (void*, ptr)(size_t, nmemb)(size_t, size))
DECLARE_FN(int, posix_memalign, (void**, memptr)(size_t, alignment)(size_t, size));
DECLARE_FN(void*, valloc, (size_t, size));

#define SET_SYMBOL(name) \
    __dd_real_##name = dlsym(RTLD_NEXT, #name);

// calloc is invoked by dlsym, returning a null value in this case is well
// handled by glibc
ALLOCATION_VISIBILITY
void* temp_calloc2(size_t nmemb, size_t size)
{
    return NULL;
}

ALLOCATION_VISIBILITY
static void init()
{
    __dd_real_calloc = &temp_calloc2;

    SET_SYMBOL(calloc);
    SET_SYMBOL(malloc);
    SET_SYMBOL(aligned_alloc);
    SET_SYMBOL(free);
    SET_SYMBOL(realloc);
    SET_SYMBOL(reallocarray);
    SET_SYMBOL(posix_memalign);
    SET_SYMBOL(valloc);
}

static once_flag flag = ONCE_FLAG_INIT;
static void check_init()
{
    call_once(&flag, init);
}

#define DEFINE_FN(return_type, name, parameters)                           \
    return_type temp_##name(END(PARAMS_LOOP_0 parameters))                 \
    {                                                                      \
        check_init();                                                      \
        return __dd_real_##name(END(VAR_LOOP_0 parameters));               \
    }                                                                      \
    return_type name(END(PARAMS_LOOP_0 parameters))                        \
    {                                                                      \
        allocation_api_usage_counter++;                                    \
        return_type result = __dd_real_##name(END(VAR_LOOP_0 parameters)); \
        allocation_api_usage_counter--;                                    \
        return result;                                                     \
    }

#define DEFINE_FN_VOID(name, parameters)              \
    void temp_##name(END(PARAMS_LOOP_0 parameters))   \
    {                                                 \
        check_init();                                 \
        __dd_real_##name(END(VAR_LOOP_0 parameters)); \
    }                                                 \
    void name(END(PARAMS_LOOP_0 parameters))          \
    {                                                 \
        allocation_api_usage_counter++;               \
        __dd_real_##name(END(VAR_LOOP_0 parameters)); \
        allocation_api_usage_counter--;               \
    }

DEFINE_FN(void*, calloc, (size_t, nmemb)(size_t, size))
DEFINE_FN(void*, malloc, (size_t, size))
DEFINE_FN(void*, aligned_alloc, (size_t, align)(size_t, size))
DEFINE_FN_VOID(free, (void*, ptr))
DEFINE_FN(void*, realloc, (void*, ptr)(size_t, size))
DEFINE_FN(void*, reallocarray, (void*, ptr)(size_t, nmemb)(size_t, size))
DEFINE_FN(int, posix_memalign, (void**, memptr)(size_t, alignment)(size_t, size));
DEFINE_FN(void*, valloc, (size_t, size));
