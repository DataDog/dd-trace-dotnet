
#include <pthread.h>

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
extern void (*volatile dd_notify_libraries_cache_update)();

int is_interrupted_by_profiler(int rc, int error_code, int interrupted_by_profiler);
int __dd_set_shared_memory(volatile int* mem);
void __dd_notify_libraries_cache_update();

void *__dd_dlsym(void *handle, const char *symbol);