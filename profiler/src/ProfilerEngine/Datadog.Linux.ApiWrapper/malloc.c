#define _GNU_SOURCE
#include <dlfcn.h>
#include <stdlib.h>

#include "common.h"

__attribute__((visibility("hidden")))
__thread unsigned int allocation_api_usage_counter = 0;

__attribute__((visibility("hidden"))) int dd_is_using_allocation_api()
{
    return allocation_api_usage_counter;
}

#define WRAP_ALLOC_FN(return_type, name, parameters)                              \
    static return_type (*__dd_real_##name)(END(PARAMS_LOOP_0 parameters)) = NULL; \
    return_type name(END(PARAMS_LOOP_0 parameters))                               \
    {                                                                             \
        if (__dd_real_##name == NULL)                                             \
        {                                                                         \
            __dd_real_##name = dlsym(RTLD_NEXT, #name);                           \
        }                                                                         \
        allocation_api_usage_counter++;                                           \
        return_type result = __dd_real_##name(END(VAR_LOOP_0 parameters));        \
        allocation_api_usage_counter--;                                           \
        return result;                                                            \
    }

#define WRAP_ALLOC_FN_VOID(name, parameters)                               \
    static void (*__dd_real_##name)(END(PARAMS_LOOP_0 parameters)) = NULL; \
    void name(END(PARAMS_LOOP_0 parameters))                               \
    {                                                                      \
        if (__dd_real_##name == NULL)                                      \
        {                                                                  \
            __dd_real_##name = dlsym(RTLD_NEXT, #name);                    \
        }                                                                  \
        allocation_api_usage_counter++;                                    \
        __dd_real_##name(END(VAR_LOOP_0 parameters));                      \
        allocation_api_usage_counter--;                                    \
    }

WRAP_ALLOC_FN(void*, malloc, (size_t, size))
WRAP_ALLOC_FN_VOID(free, (void*, ptr))
WRAP_ALLOC_FN(void*, calloc, (size_t, nmemb)(size_t, size))
WRAP_ALLOC_FN(void*, realloc, (void*, ptr)(size_t, size))
WRAP_ALLOC_FN(void*, reallocarray, (void*, ptr)(size_t, nmemb)(size_t, size))