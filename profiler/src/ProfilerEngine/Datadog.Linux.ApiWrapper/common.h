#pragma once

#include <pthread.h>

// TEMPORARY diagnostic instrumentation to root-cause the OpenLDAP / wall-time-profiler
// interaction (see OpenLdapTests.CheckOpenLdapCrash). Remove once resolved.
// Deliberately a compile-time constant, not a new DD_* env var: env vars need
// registering in the config registry, which isn't worth it for a throwaway
// investigation flag. Flip to 0 (or delete this whole diagnostic) to disable.
// Shared via this header so socket_operations.c and filesystem_operations.c agree.
#define DD_TRACE_SYSCALLS_SHIELD 1

#define END(...) END_(__VA_ARGS__)
// cppcheck-suppress preprocessorErrorDirective
#define END_(...) __VA_ARGS__##_END

#define PARAMS_LOOP_0(type_, name_) PARAMS_LOOP_BODY(type_, name_) PARAMS_LOOP_A
#define PARAMS_LOOP_A(type_, name_) , PARAMS_LOOP_BODY(type_, name_) PARAMS_LOOP_B
#define PARAMS_LOOP_B(type_, name_) , PARAMS_LOOP_BODY(type_, name_) PARAMS_LOOP_A
#define PARAMS_LOOP_0_END
#define PARAMS_LOOP_A_END
#define PARAMS_LOOP_B_END
#define PARAMS_LOOP_BODY(type_, name_) type_ name_

#define VAR_LOOP_0(type_, name_) name_ VAR_LOOP_A
#define VAR_LOOP_A(type_, name_) , name_ VAR_LOOP_B
#define VAR_LOOP_B(type_, name_) , name_ VAR_LOOP_A
#define VAR_LOOP_0_END
#define VAR_LOOP_A_END
#define VAR_LOOP_B_END

#ifdef __GLIBC__
#define DD_CONST
#if __GLIBC__ == 2 && __GLIBC_MINOR__ < 21
#undef DD_CONST
#define DD_CONST const
#endif
#endif

int __dd_pthread_once(pthread_once_t *control, void (*init)(void));

extern int (*volatile dd_set_shared_memory)(volatile int*);

int is_interrupted_by_profiler(int rc, int error_code, int interrupted_by_profiler);
int __dd_set_shared_memory(volatile int* mem);
void __dd_notify_libraries_cache_update();
void __dd_on_thread_routine_finished();

void *__dd_dlsym(void *handle, const char *symbol);